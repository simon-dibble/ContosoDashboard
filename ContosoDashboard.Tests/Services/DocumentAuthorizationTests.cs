using ContosoDashboard.Services;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace ContosoDashboard.Tests.Services;

public class DocumentAuthorizationTests
{
    [Fact]
    public void Operations_have_explicit_non_disclosing_contract()
    {
        Assert.False(new DocumentAuthorizationDecision(false).Allowed);
        Assert.Contains(DocumentOperation.Download, Enum.GetValues<DocumentOperation>());
        Assert.Contains(DocumentOperation.AuditReport, Enum.GetValues<DocumentOperation>());
    }

    [Fact]
    public void Denied_decisions_do_not_carry_resource_metadata()
    {
        var decision = new DocumentAuthorizationDecision(false);

        Assert.False(decision.Allowed);
        Assert.Single(decision.GetType().GetProperties());
    }

    [Fact]
    public void Every_protected_operation_is_explicitly_named()
    {
        var operations = Enum.GetValues<DocumentOperation>();

        Assert.Contains(DocumentOperation.ReadMetadata, operations);
        Assert.Contains(DocumentOperation.Search, operations);
        Assert.Contains(DocumentOperation.Download, operations);
        Assert.Contains(DocumentOperation.Delete, operations);
    }

    [Fact]
    public async Task Project_member_sees_project_document_but_unrelated_user_does_not()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new ApplicationDbContext(options);
        context.Users.AddRange(
            new User { UserId = 10, Email = "member@test", DisplayName = "Member" },
            new User { UserId = 11, Email = "owner@test", DisplayName = "Owner" },
            new User { UserId = 12, Email = "other@test", DisplayName = "Other" });
        context.Projects.Add(new Project { ProjectId = 5, Name = "Project", ProjectManagerId = 10 });
        context.ProjectMembers.Add(new ProjectMember { ProjectMemberId = 1, ProjectId = 5, UserId = 10 });
        context.Documents.Add(new Document
        {
            DocumentId = 9, Title = "Private title", Category = "Other", StorageName = "opaque-name",
            OriginalFileName = "private.txt", FileSize = 1, FileType = "text/plain",
            UploadedByUserId = 11, ProjectId = 5, ScanStatus = DocumentScanStatus.Passed
        });
        await context.SaveChangesAsync();

        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var authorization = new DocumentAuthorizationService(context, accessor);

        Assert.True((await authorization.AuthorizeAsync(10, 9, DocumentOperation.ReadMetadata)).Allowed);
        Assert.False((await authorization.AuthorizeAsync(12, 9, DocumentOperation.ReadMetadata)).Allowed);
        Assert.False((await authorization.AuthorizeAsync(10, null, DocumentOperation.ReadMetadata)).Allowed);
    }

    [Fact]
    public async Task Team_lead_and_task_viewer_are_re_evaluated_from_live_relationships()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new ApplicationDbContext(options);
        context.Users.AddRange(
            new User { UserId = 20, Email = "lead@test", DisplayName = "Lead", Department = "Engineering", Role = UserRole.TeamLead },
            new User { UserId = 21, Email = "owner@test", DisplayName = "Owner", Department = "Engineering" },
            new User { UserId = 22, Email = "viewer@test", DisplayName = "Viewer" });
        context.Tasks.Add(new TaskItem
        {
            TaskId = 30, Title = "Task", AssignedUserId = 22, CreatedByUserId = 21
        });
        context.Documents.Add(new Document
        {
            DocumentId = 31, Title = "Task document", Category = "Other", StorageName = "opaque-name",
            OriginalFileName = "task.txt", FileSize = 1, FileType = "text/plain",
            UploadedByUserId = 21, ScanStatus = DocumentScanStatus.Passed
        });
        context.DocumentTaskAssociations.Add(new DocumentTaskAssociation
        {
            DocumentTaskAssociationId = 32, DocumentId = 31, TaskId = 30, AttachedByUserId = 21
        });
        await context.SaveChangesAsync();

        var authorization = new DocumentAuthorizationService(context, new HttpContextAccessor());

        Assert.True((await authorization.AuthorizeAsync(20, 31, DocumentOperation.ReadMetadata)).Allowed);
        Assert.True((await authorization.AuthorizeAsync(22, 31, DocumentOperation.ReadMetadata)).Allowed);
        Assert.False((await authorization.AuthorizeAsync(22, 31, DocumentOperation.Edit)).Allowed);

        context.Users.Single(u => u.UserId == 20).Department = "Sales";
        await context.SaveChangesAsync();
        Assert.False((await authorization.AuthorizeAsync(20, 31, DocumentOperation.ReadMetadata)).Allowed);
    }
}
