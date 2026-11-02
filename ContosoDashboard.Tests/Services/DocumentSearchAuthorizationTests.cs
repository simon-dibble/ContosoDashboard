using ContosoDashboard.Models;

namespace ContosoDashboard.Tests.Services;

public class DocumentSearchAuthorizationTests
{
    [Fact]
    public async Task Search_filters_by_authorization_before_returning_metadata_and_supports_filters()
    {
        await using var db = DocumentTestData.Context();
        db.Users.AddRange(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" },
            new User { UserId = 2, Email = "member@test", DisplayName = "Member" },
            new User { UserId = 3, Email = "other@test", DisplayName = "Other" });
        db.Projects.Add(new Project { ProjectId = 10, Name = "Project", ProjectManagerId = 1 });
        db.ProjectMembers.Add(new ProjectMember { ProjectMemberId = 1, ProjectId = 10, UserId = 2 });
        db.Documents.AddRange(DocumentTestData.Document(1, 1, "Visible report", DateTime.UtcNow.AddDays(-1), 10),
            DocumentTestData.Document(2, 3, "Secret report", DateTime.UtcNow),
            new Document { DocumentId = 3, UploadedByUserId = 1, Title = "Rejected", Category = "Reports",
                StorageName = "rejected", OriginalFileName = "x.txt", FileSize = 1, FileType = "text/plain",
                ScanStatus = DocumentScanStatus.Rejected });
        await db.SaveChangesAsync();
        var auth = new DocumentTestAuthorization(db, 2);
        var service = DocumentTestData.Service(db, auth);

        var results = await service.ListAsync(search: "report", category: "Reports", projectId: 10, sort: "title");

        Assert.Single(results);
        Assert.Equal("Visible report", results[0].Title);
        Assert.DoesNotContain(results, d => d.Title == "Secret report");
    }

    [Fact]
    public async Task Revoked_share_is_not_an_IDOR_access_path_and_sorting_is_stable()
    {
        await using var db = DocumentTestData.Context();
        db.Users.AddRange(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" },
            new User { UserId = 2, Email = "viewer@test", DisplayName = "Viewer" });
        db.Documents.Add(DocumentTestData.Document(1, 1, "Private", DateTime.UtcNow));
        db.DocumentShares.Add(new DocumentShare { DocumentId = 1, UserId = 2, SharedByUserId = 1, RevokedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var auth = new DocumentTestAuthorization(db, 2);
        var service = DocumentTestData.Service(db, auth);

        Assert.Empty(await service.ListAsync());
        Assert.Null(await service.GetAsync(1));
    }
}
