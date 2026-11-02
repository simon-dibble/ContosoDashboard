using ContosoDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Tests.Services;

public class DocumentTaskAssociationTests
{
    [Fact]
    public async Task Authorized_same_project_attach_is_idempotent_and_cross_project_is_denied()
    {
        await using var db = DocumentTestData.Context();
        db.Users.Add(new User { UserId = 1, Email = "member@test", DisplayName = "Member" });
        db.Projects.AddRange(new Project { ProjectId = 10, Name = "A", ProjectManagerId = 1 },
            new Project { ProjectId = 11, Name = "B", ProjectManagerId = 1 });
        db.ProjectMembers.Add(new ProjectMember { ProjectMemberId = 1, ProjectId = 10, UserId = 1 });
        db.Tasks.AddRange(new TaskItem { TaskId = 20, Title = "A task", AssignedUserId = 1, CreatedByUserId = 1, ProjectId = 10 },
            new TaskItem { TaskId = 21, Title = "B task", AssignedUserId = 1, CreatedByUserId = 1, ProjectId = 11 });
        db.Documents.Add(DocumentTestData.Document(1, 1, "Doc", DateTime.UtcNow, 10));
        await db.SaveChangesAsync();
        var service = DocumentTestData.Service(db, new DocumentTestAuthorization(db, 1));

        Assert.True((await service.AttachToTaskAsync(1, 20)).Succeeded);
        Assert.True((await service.AttachToTaskAsync(1, 20)).Succeeded);
        Assert.False((await service.AttachToTaskAsync(1, 21)).Succeeded);
        Assert.Single(await db.DocumentTaskAssociations.ToListAsync());
    }

    [Fact]
    public async Task Unauthorized_viewer_cannot_attach_or_view_document()
    {
        await using var db = DocumentTestData.Context();
        db.Users.AddRange(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" },
            new User { UserId = 2, Email = "other@test", DisplayName = "Other" });
        db.Tasks.Add(new TaskItem { TaskId = 20, Title = "Task", AssignedUserId = 1, CreatedByUserId = 1 });
        db.Documents.Add(DocumentTestData.Document(1, 1, "Private", DateTime.UtcNow));
        await db.SaveChangesAsync();
        var service = DocumentTestData.Service(db, new DocumentTestAuthorization(db, 2));

        Assert.False((await service.AttachToTaskAsync(1, 20)).Succeeded);
        Assert.Null(await service.GetAsync(1));
    }
}
