using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Tests.Services;

public class DocumentMaintenanceTests
{
    [Fact]
    public async Task Metadata_validation_and_successful_replacement_preserve_document_identity()
    {
        await using var db = DocumentTestData.Context();
        db.Users.Add(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" });
        db.Documents.Add(DocumentTestData.Document(1, 1, "Original", DateTime.UtcNow));
        await db.SaveChangesAsync();
        var auth = new DocumentTestAuthorization(db, 1);
        var storage = new MemoryDocumentStorage();
        var service = DocumentTestData.Service(db, auth, storage);

        Assert.Null(await service.UpdateMetadataAsync(1, " ", null, "Reports", null));
        var updated = await service.UpdateMetadataAsync(1, "Renamed", "new description", "Reports", ["new"]);
        var replacement = await service.ReplaceAsync(1,
            new DocumentUploadRequest("replacement.txt", "text/plain", new MemoryStream([1, 2, 3]), 3));

        Assert.Equal("Renamed", updated!.Title);
        Assert.True(replacement.Succeeded);
        Assert.Equal("replacement.txt", (await service.GetAsync(1))!.OriginalFileName);
        Assert.Single(await db.Documents.ToListAsync());
    }

    [Fact]
    public async Task Rejected_replacement_does_not_change_previous_passed_content()
    {
        await using var db = DocumentTestData.Context();
        db.Users.Add(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" });
        db.Documents.Add(DocumentTestData.Document(1, 1, "Original", DateTime.UtcNow));
        await db.SaveChangesAsync();
        var service = DocumentTestData.Service(db, new DocumentTestAuthorization(db, 1));

        var result = await service.ReplaceAsync(1,
            new DocumentUploadRequest("eicar.txt", "text/plain",
                new MemoryStream(System.Text.Encoding.UTF8.GetBytes("EICAR-STANDARD-ANTIVIRUS-TEST-FILE")), 36));

        Assert.False(result.Succeeded);
        Assert.Equal("opaque-1", (await db.Documents.FindAsync(1))!.StorageName);
    }

    [Fact]
    public async Task Storage_failure_during_replacement_retains_previous_passed_content()
    {
        await using var db = DocumentTestData.Context();
        db.Users.Add(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" });
        db.Documents.Add(DocumentTestData.Document(1, 1, "Original", DateTime.UtcNow));
        await db.SaveChangesAsync();
        var service = new DocumentService(db, new FailingOnCommitStorage(), new DeterministicMalwareScanner(), new DocumentTestAuthorization(db, 1), new DocumentValidationService(), new NotificationService(db), Microsoft.Extensions.Logging.Abstractions.NullLogger<DocumentService>.Instance, new DocumentAuditService(db));

        var result = await service.ReplaceAsync(1,
            new DocumentUploadRequest("replacement.txt", "text/plain", new MemoryStream([1, 2, 3]), 3));

        Assert.False(result.Succeeded);
        var document = await db.Documents.FindAsync(1);
        Assert.Equal("opaque-1", document!.StorageName);
        Assert.Equal("Original.txt", document.OriginalFileName);
    }

    [Fact]
    public async Task Project_manager_can_edit_but_non_member_cannot()
    {
        await using var db = DocumentTestData.Context();
        db.Users.AddRange(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" },
            new User { UserId = 2, Email = "manager@test", DisplayName = "Manager" },
            new User { UserId = 3, Email = "other@test", DisplayName = "Other" });
        db.Projects.Add(new Project { ProjectId = 10, Name = "Project", ProjectManagerId = 2 });
        db.Documents.Add(DocumentTestData.Document(1, 1, "Original", DateTime.UtcNow, 10));
        await db.SaveChangesAsync();

        var managerService = DocumentTestData.Service(db, new DocumentTestAuthorization(db, 2));
        var otherService = DocumentTestData.Service(db, new DocumentTestAuthorization(db, 3));

        Assert.NotNull(await managerService.UpdateMetadataAsync(1, "Manager edit", null, "Reports", null));
        Assert.Null(await otherService.UpdateMetadataAsync(1, "Unauthorized", null, "Reports", null));
    }

    private sealed class FailingOnCommitStorage : IFileStorage
    {
        public Task<TemporaryStorageHandle> CreateTemporaryAsync(Stream content, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TemporaryStorageHandle("tmp-" + Guid.NewGuid().ToString("N")));
        public Task<CommittedStorageHandle> CommitAsync(TemporaryStorageHandle temporaryHandle, string opaqueStorageName, CancellationToken cancellationToken = default) =>
            throw new IOException("Simulated storage failure.");
        public Task<Stream> OpenReadAsync(string opaqueStorageName, CancellationToken cancellationToken = default) =>
            throw new FileNotFoundException();
        public Task<bool> DeleteAsync(string opaqueStorageName, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}

