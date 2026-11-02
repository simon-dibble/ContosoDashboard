using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Tests.Services;

// NOTE: DocumentService.ShareAsync accepts an explicit list of user IDs (plus an optional
// SourceTeam label persisted on the share for audit purposes). There is no automatic
// department/project-team membership expansion and no per-user notification preference
// filtering implemented anywhere in the codebase today, so those specific parts of task
// T032's acceptance criteria are intentionally not exercised here; see tasks.md for the
// explanation recorded against T032.
public class DocumentSharingTests
{
    [Fact]
    public async Task Owner_can_share_with_selected_users_deduplicating_existing_active_grants()
    {
        await using var db = DocumentTestData.Context();
        db.Users.AddRange(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" },
            new User { UserId = 2, Email = "a@test", DisplayName = "A" },
            new User { UserId = 3, Email = "b@test", DisplayName = "B" });
        db.Documents.Add(DocumentTestData.Document(1, 1, "Shared doc", DateTime.UtcNow));
        await db.SaveChangesAsync();
        var service = DocumentTestData.Service(db, new DocumentTestAuthorization(db, 1));

        var first = await service.ShareAsync(1, new DocumentShareRequest([2, 3, 2], "Engineering"));
        var second = await service.ShareAsync(1, new DocumentShareRequest([2, 3]));

        Assert.Equal(2, first.Count);
        Assert.Equal(2, second.Count);
        Assert.Equal(2, await db.DocumentShares.CountAsync(s => s.DocumentId == 1 && s.RevokedAt == null));
        Assert.Equal(2, await db.Notifications.CountAsync(n => n.Type == NotificationType.DocumentShared));
    }

    [Fact]
    public async Task Revoked_share_no_longer_grants_access_but_remains_for_audit()
    {
        await using var db = DocumentTestData.Context();
        db.Users.AddRange(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" },
            new User { UserId = 2, Email = "shared@test", DisplayName = "Shared" });
        db.Documents.Add(DocumentTestData.Document(1, 1, "Shared doc", DateTime.UtcNow));
        await db.SaveChangesAsync();
        var ownerService = DocumentTestData.Service(db, new DocumentTestAuthorization(db, 1));
        await ownerService.ShareAsync(1, new DocumentShareRequest([2]));
        var viewerService = DocumentTestData.Service(db, new DocumentTestAuthorization(db, 2));
        Assert.NotNull(await viewerService.GetAsync(1));

        var revoked = await ownerService.RevokeShareAsync(1, 2);

        Assert.True(revoked);
        Assert.Null(await viewerService.GetAsync(1));
        Assert.Single(await db.DocumentShares.ToListAsync());
    }

    [Fact]
    public async Task Shared_with_me_excludes_revoked_and_unauthorized_rejected_documents()
    {
        await using var db = DocumentTestData.Context();
        db.Users.AddRange(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" },
            new User { UserId = 2, Email = "viewer@test", DisplayName = "Viewer" });
        db.Documents.Add(DocumentTestData.Document(1, 1, "Active share", DateTime.UtcNow));
        db.Documents.Add(DocumentTestData.Document(2, 1, "Revoked share", DateTime.UtcNow));
        db.DocumentShares.Add(new DocumentShare { DocumentId = 1, UserId = 2, SharedByUserId = 1 });
        db.DocumentShares.Add(new DocumentShare { DocumentId = 2, UserId = 2, SharedByUserId = 1, RevokedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var service = DocumentTestData.Service(db, new DocumentTestAuthorization(db, 2));

        var sharedWithMe = await service.GetSharedWithMeAsync();

        Assert.Single(sharedWithMe);
        Assert.Equal("Active share", sharedWithMe[0].Title);
    }

    [Fact]
    public async Task Non_owner_non_manager_cannot_share_or_revoke()
    {
        await using var db = DocumentTestData.Context();
        db.Users.AddRange(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" },
            new User { UserId = 2, Email = "other@test", DisplayName = "Other" },
            new User { UserId = 3, Email = "target@test", DisplayName = "Target" });
        db.Documents.Add(DocumentTestData.Document(1, 1, "Private", DateTime.UtcNow));
        await db.SaveChangesAsync();
        var ownerService = DocumentTestData.Service(db, new DocumentTestAuthorization(db, 1));
        await ownerService.ShareAsync(1, new DocumentShareRequest([3]));
        var otherService = DocumentTestData.Service(db, new DocumentTestAuthorization(db, 2));

        var shareAttempt = await otherService.ShareAsync(1, new DocumentShareRequest([3]));
        var revokeAttempt = await otherService.RevokeShareAsync(1, 3);

        Assert.Empty(shareAttempt);
        Assert.False(revokeAttempt);
    }
}
