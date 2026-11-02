using ContosoDashboard.Models;

namespace ContosoDashboard.Tests.Services;

public class DocumentAccessTests
{
    [Fact]
    public async Task Authorized_passed_document_can_be_opened_and_audit_is_recorded()
    {
        await using var db = DocumentTestData.Context();
        db.Users.Add(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" });
        var storage = new MemoryDocumentStorage();
        await using var input = new MemoryStream(new byte[] { 1, 2, 3 });
        var temporary = await storage.CreateTemporaryAsync(input);
        var committed = await storage.CommitAsync(temporary, "opaque-1");
        db.Documents.Add(DocumentTestData.Document(1, 1, "Report", DateTime.UtcNow));
        await db.SaveChangesAsync();
        var service = DocumentTestData.Service(db, new DocumentTestAuthorization(db, 1), storage);

        var result = await service.OpenReadAsync(1);

        Assert.NotNull(result);
        Assert.Equal(new byte[] { 1, 2, 3 }, ((MemoryStream)result!.Content).ToArray());
        Assert.Contains(db.DocumentActivities, a => a.Action == "Download" && a.Outcome == "Succeeded");
    }

    [Fact]
    public async Task Rejected_documents_are_not_readable_and_revoked_share_loses_access()
    {
        await using var db = DocumentTestData.Context();
        db.Users.AddRange(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" },
            new User { UserId = 2, Email = "shared@test", DisplayName = "Shared" });
        db.Documents.Add(new Document { DocumentId = 2, UploadedByUserId = 1, Title = "Rejected", Category = "Reports",
            StorageName = "opaque-2", OriginalFileName = "x.pdf", FileSize = 1, FileType = "application/pdf", ScanStatus = DocumentScanStatus.Rejected });
        db.Documents.Add(DocumentTestData.Document(3, 1, "Shared", DateTime.UtcNow));
        db.DocumentShares.Add(new DocumentShare { DocumentId = 3, UserId = 2, SharedByUserId = 1 });
        await db.SaveChangesAsync();

        var rejected = DocumentTestData.Service(db, new DocumentTestAuthorization(db, 1));
        Assert.Null(await rejected.GetAsync(2));

        var auth = new DocumentTestAuthorization(db, 2);
        var service = DocumentTestData.Service(db, auth);
        Assert.NotNull(await service.GetAsync(3));
        db.DocumentShares.Single().RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        Assert.Null(await service.GetAsync(3));
    }

    [Fact]
    public async Task Pending_scan_documents_are_not_readable_until_passed()
    {
        await using var db = DocumentTestData.Context();
        db.Users.Add(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" });
        db.Documents.Add(new Document { DocumentId = 4, UploadedByUserId = 1, Title = "Still scanning", Category = "Reports",
            StorageName = "opaque-4", OriginalFileName = "pending.pdf", FileSize = 1, FileType = "application/pdf", ScanStatus = DocumentScanStatus.Pending });
        await db.SaveChangesAsync();
        var service = DocumentTestData.Service(db, new DocumentTestAuthorization(db, 1));

        Assert.Null(await service.GetAsync(4));
        Assert.Null(await service.OpenReadAsync(4));
    }

    [Fact]
    public async Task Missing_document_returns_null_without_recording_a_download()
    {
        await using var db = DocumentTestData.Context();
        db.Users.Add(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" });
        await db.SaveChangesAsync();
        var service = DocumentTestData.Service(db, new DocumentTestAuthorization(db, 1));

        Assert.Null(await service.GetAsync(999));
        Assert.Null(await service.OpenReadAsync(999));
        Assert.DoesNotContain(db.DocumentActivities, a => a.Action == "Download");
    }
}
