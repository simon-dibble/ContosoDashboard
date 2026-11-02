using ContosoDashboard.Models;
using ContosoDashboard.Services;

namespace ContosoDashboard.Tests.Services;

public class DocumentAuditReportTests
{
    [Fact]
    public async Task Report_aggregates_most_uploaded_file_types_and_most_active_uploaders()
    {
        await using var db = DocumentTestData.Context();
        db.Users.AddRange(
            new User { UserId = 1, Email = "uploader1@test", DisplayName = "Uploader One" },
            new User { UserId = 2, Email = "uploader2@test", DisplayName = "Uploader Two" },
            new User { UserId = 3, Email = "admin@test", DisplayName = "Admin", Role = UserRole.Administrator });
        await db.SaveChangesAsync();
        var audit = new DocumentAuditService(db);

        var pdf1 = DocumentTestData.Document(1, 1, "Report A", DateTime.UtcNow);
        var pdf2 = DocumentTestData.Document(2, 1, "Report B", DateTime.UtcNow);
        db.Documents.AddRange(pdf1, pdf2);
        await db.SaveChangesAsync();

        await audit.RecordAsync(1, "Upload", "Succeeded", new Document { DocumentId = 1, Title = "Report A", FileType = "application/pdf" });
        await audit.RecordAsync(1, "Upload", "Succeeded", new Document { DocumentId = 2, Title = "Report B", FileType = "application/pdf" });
        await audit.RecordAsync(2, "Upload", "Succeeded", new Document { DocumentId = 3, Title = "Photo A", FileType = "image/png" });
        await audit.RecordAsync(1, "Download", "Succeeded", new Document { DocumentId = 1, Title = "Report A", FileType = "application/pdf" });
        await audit.RecordAsync(1, "Upload", "Rejected", new Document { DocumentId = 4, Title = "Bad file", FileType = "application/octet-stream" });

        var report = await audit.GetReportAsync(3);

        Assert.NotNull(report);
        Assert.Contains(report!.MostUploadedFileTypes, s => s.FileType == "application/pdf" && s.Count == 2);
        Assert.Contains(report.MostUploadedFileTypes, s => s.FileType == "image/png" && s.Count == 1);
        Assert.DoesNotContain(report.MostUploadedFileTypes, s => s.FileType == "application/octet-stream");

        Assert.Contains(report.MostActiveUploaders, s => s.ActorUserId == 1 && s.Count == 2);
        Assert.Contains(report.MostActiveUploaders, s => s.ActorUserId == 2 && s.Count == 1);
    }

    [Fact]
    public async Task Report_is_denied_to_non_administrators()
    {
        await using var db = DocumentTestData.Context();
        db.Users.Add(new User { UserId = 1, Email = "employee@test", DisplayName = "Employee" });
        await db.SaveChangesAsync();
        var audit = new DocumentAuditService(db);

        Assert.Null(await audit.GetReportAsync(1));
    }

    [Fact]
    public async Task Report_take_parameter_is_bounded_to_a_safe_range()
    {
        await using var db = DocumentTestData.Context();
        db.Users.Add(new User { UserId = 1, Email = "admin@test", DisplayName = "Admin", Role = UserRole.Administrator });
        await db.SaveChangesAsync();
        var audit = new DocumentAuditService(db);
        for (var i = 0; i < 5; i++)
            await audit.RecordAsync(1, "Upload", "Succeeded", new Document { DocumentId = i, Title = $"Doc {i}", FileType = "text/plain" });

        var reportWithNegativeTake = await audit.GetReportAsync(1, take: -5);
        var reportWithHugeTake = await audit.GetReportAsync(1, take: 10_000);

        Assert.NotNull(reportWithNegativeTake);
        Assert.NotNull(reportWithHugeTake);
        Assert.True(reportWithNegativeTake!.Activities.Count <= 5);
        Assert.True(reportWithHugeTake!.Activities.Count <= 5);
    }
}

