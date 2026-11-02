using ContosoDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Tests.Services;

public class DocumentDeletionTests
{
    [Fact]
    public async Task Delete_requires_explicit_confirmation_and_missing_documents_are_non_disclosing()
    {
        await using var db = DocumentTestData.Context();
        db.Users.Add(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" });
        db.Documents.Add(DocumentTestData.Document(1, 1, "Sensitive title", DateTime.UtcNow));
        await db.SaveChangesAsync();
        var service = DocumentTestData.Service(db, new DocumentTestAuthorization(db, 1));

        var unconfirmed = await service.DeleteAsync(1, false);
        var missing = await service.DeleteAsync(99, true);

        Assert.False(unconfirmed.Succeeded);
        Assert.Contains("Confirmation", unconfirmed.Error);
        Assert.False(missing.Succeeded);
        Assert.DoesNotContain("Sensitive", missing.Error ?? "");
    }

    [Fact]
    public async Task Confirmed_delete_permanently_removes_document_and_content_and_retains_an_audit_snapshot()
    {
        await using var db = DocumentTestData.Context();
        db.Users.Add(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" });
        db.Documents.Add(DocumentTestData.Document(1, 1, "Final report", DateTime.UtcNow));
        await db.SaveChangesAsync();
        var storage = new MemoryDocumentStorage();
        await using var input = new MemoryStream(new byte[] { 9, 9, 9 });
        var temporary = await storage.CreateTemporaryAsync(input);
        await storage.CommitAsync(temporary, "opaque-1");
        var service = DocumentTestData.Service(db, new DocumentTestAuthorization(db, 1), storage);

        var result = await service.DeleteAsync(1, true);

        Assert.True(result.Succeeded);
        Assert.Null(await db.Documents.FindAsync(1));
        Assert.Empty(await db.Documents.ToListAsync());
        await Assert.ThrowsAnyAsync<Exception>(() => storage.OpenReadAsync("opaque-1"));
        var activity = Assert.Single(db.DocumentActivities, a => a.Action == "Delete");
        Assert.Null(activity.DocumentId);
        Assert.Equal("Final report", activity.DocumentTitleSnapshot);
        Assert.Equal("text/plain", activity.FileTypeSnapshot);
        Assert.Equal("Succeeded", activity.Outcome);
        Assert.DoesNotContain("opaque-1", activity.DocumentTitleSnapshot + (activity.ReasonCode ?? ""));
    }

    [Fact]
    public async Task Non_owner_non_manager_cannot_delete_a_document()
    {
        await using var db = DocumentTestData.Context();
        db.Users.AddRange(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" },
            new User { UserId = 2, Email = "other@test", DisplayName = "Other" });
        db.Documents.Add(DocumentTestData.Document(1, 1, "Private", DateTime.UtcNow));
        await db.SaveChangesAsync();
        var service = DocumentTestData.Service(db, new DocumentTestAuthorization(db, 2));

        var result = await service.DeleteAsync(1, true);

        Assert.False(result.Succeeded);
        Assert.NotNull(await db.Documents.FindAsync(1));
    }
}
