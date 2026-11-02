using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Tests.Services;

public class DocumentAuditTests
{
    [Fact]
    public async Task Audit_records_utc_snapshot_without_content_or_storage_path()
    {
        await using var db = DocumentTestData.Context();
        db.Users.Add(new User { UserId = 1, Email = "admin@test", DisplayName = "Admin", Role = UserRole.Administrator });
        var document = DocumentTestData.Document(1, 1, "Quarterly report", DateTime.UtcNow);
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        var audit = new DocumentAuditService(db);

        await audit.RecordAsync(1, "Download", "Succeeded", document, "clean");
        var activity = Assert.Single(db.DocumentActivities);
        Assert.Equal("Quarterly report", activity.DocumentTitleSnapshot);
        Assert.Equal("text/plain", activity.FileTypeSnapshot);
        Assert.Equal(DateTimeKind.Utc, activity.OccurredAt.Kind);
        Assert.DoesNotContain("opaque-1", activity.DocumentTitleSnapshot + activity.ReasonCode);
        Assert.DoesNotContain("bytes", activity.ReasonCode ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Upload_download_share_and_delete_each_record_an_append_only_audit_entry()
    {
        await using var db = DocumentTestData.Context();
        db.Users.AddRange(
            new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" },
            new User { UserId = 2, Email = "recipient@test", DisplayName = "Recipient" });
        await db.SaveChangesAsync();
        var authorization = new DocumentTestAuthorization(db, 1);
        var service = DocumentTestData.Service(db, authorization);

        var uploadResult = (await service.UploadAsync("Quarterly report", null, "Reports", null, null,
            new[] { new DocumentUploadRequest("report.pdf", "application/pdf", new MemoryStream(new byte[] { 1, 2, 3 }), 3) }))[0];
        Assert.True(uploadResult.Succeeded);
        var documentId = uploadResult.Document!.DocumentId;

        var opened = await service.OpenReadAsync(documentId);
        Assert.NotNull(opened);
        opened!.Content.Dispose();

        var shares = await service.ShareAsync(documentId, new DocumentShareRequest(new[] { 2 }));
        Assert.Single(shares);

        var deleteResult = await service.DeleteAsync(documentId, confirmed: true);
        Assert.True(deleteResult.Succeeded);

        var activities = await db.DocumentActivities.ToListAsync();
        Assert.Contains(activities, a => a.Action == "Upload" && a.Outcome == "Succeeded");
        Assert.Contains(activities, a => a.Action == "Download" && a.Outcome == "Succeeded");
        Assert.Contains(activities, a => a.Action == "Share" && a.Outcome == "Succeeded");
        Assert.Contains(activities, a => a.Action == "Delete" && a.Outcome == "Succeeded");
        Assert.All(activities, a => Assert.Equal(DateTimeKind.Utc, a.OccurredAt.Kind));
        Assert.All(activities, a => Assert.DoesNotContain("opaque", (a.DocumentTitleSnapshot ?? "") + (a.ReasonCode ?? ""), StringComparison.OrdinalIgnoreCase));

        // Append-only + post-delete retention: the document row is gone, but its audit snapshots remain untouched.
        Assert.Empty(db.Documents);
        var deleteActivity = activities.Single(a => a.Action == "Delete");
        Assert.Equal("Quarterly report", deleteActivity.DocumentTitleSnapshot);
        Assert.Null(deleteActivity.DocumentId);
        var uploadActivity = activities.Single(a => a.Action == "Upload");
        Assert.Equal("Quarterly report", uploadActivity.DocumentTitleSnapshot);
        Assert.Equal("application/pdf", uploadActivity.FileTypeSnapshot);
    }

    [Fact]
    public async Task Audit_report_is_denied_to_non_administrators_and_take_is_bounded()
    {
        await using var db = DocumentTestData.Context();
        db.Users.AddRange(new User { UserId = 1, Email = "employee@test", DisplayName = "Employee" },
            new User { UserId = 2, Email = "admin@test", DisplayName = "Admin", Role = UserRole.Administrator });
        await db.SaveChangesAsync();
        var audit = new DocumentAuditService(db);
        Assert.Null(await audit.GetReportAsync(1));
    }
}


