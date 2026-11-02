using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Tests.Security;

/// <summary>
/// End-to-end IDOR (insecure direct object reference) coverage: for every <see cref="DocumentOperation"/>,
/// confirms the real <see cref="DocumentAuthorizationService"/> grants access only to roles that should have
/// it and denies every other guessed/enumerated role, including a non-member guessing the document ID and a
/// user whose share has been revoked.
/// </summary>
public class DocumentIdorSecurityTests
{
    private const int OwnerId = 1;
    private const int ProjectManagerId = 2;
    private const int ProjectMemberId = 3;
    private const int SameDepartmentTeamLeadId = 4;
    private const int AdministratorId = 5;
    private const int OutsiderId = 6;
    private const int RevokedShareUserId = 7;
    private const int ActiveShareUserId = 8;
    private const int DocumentId = 100;
    private const int GuessedMissingDocumentId = 999999;

    private static readonly DocumentOperation[] AllDocumentOperations =
        Enum.GetValues<DocumentOperation>().Where(o => o != DocumentOperation.AuditReport).ToArray();

    private static readonly DocumentOperation[] ReadOperations =
        [DocumentOperation.ReadMetadata, DocumentOperation.Search, DocumentOperation.Preview, DocumentOperation.Download, DocumentOperation.Attach];

    private static readonly DocumentOperation[] MutateOperations =
        [DocumentOperation.Edit, DocumentOperation.Replace, DocumentOperation.Share, DocumentOperation.Delete];

    private static async Task<DocumentAuthorizationService> BuildAuthorizationAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var context = new ApplicationDbContext(options);
        context.Users.AddRange(
            new User { UserId = OwnerId, Email = "owner@test", DisplayName = "Owner", Department = "Engineering" },
            new User { UserId = ProjectManagerId, Email = "manager@test", DisplayName = "Manager" },
            new User { UserId = ProjectMemberId, Email = "member@test", DisplayName = "Member" },
            new User { UserId = SameDepartmentTeamLeadId, Email = "lead@test", DisplayName = "Lead", Department = "Engineering", Role = UserRole.TeamLead },
            new User { UserId = AdministratorId, Email = "admin@test", DisplayName = "Admin", Role = UserRole.Administrator },
            new User { UserId = OutsiderId, Email = "outsider@test", DisplayName = "Outsider" },
            new User { UserId = RevokedShareUserId, Email = "revoked@test", DisplayName = "Revoked" },
            new User { UserId = ActiveShareUserId, Email = "active@test", DisplayName = "Active" });
        context.Projects.Add(new Project { ProjectId = 1, Name = "Project", ProjectManagerId = ProjectManagerId });
        context.ProjectMembers.Add(new ProjectMember { ProjectId = 1, UserId = ProjectMemberId });
        context.Documents.Add(new Document
        {
            DocumentId = DocumentId, Title = "Confidential report", Category = "Reports", StorageName = "opaque-100",
            OriginalFileName = "report.pdf", FileSize = 10, FileType = "application/pdf",
            UploadedByUserId = OwnerId, ProjectId = 1, ScanStatus = DocumentScanStatus.Passed
        });
        context.DocumentShares.AddRange(
            new DocumentShare { DocumentId = DocumentId, UserId = RevokedShareUserId, SharedByUserId = OwnerId, RevokedAt = DateTime.UtcNow },
            new DocumentShare { DocumentId = DocumentId, UserId = ActiveShareUserId, SharedByUserId = OwnerId, RevokedAt = null });
        await context.SaveChangesAsync();

        return new DocumentAuthorizationService(context, new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
    }

    [Theory]
    [InlineData(OwnerId)]
    [InlineData(ProjectManagerId)]
    [InlineData(ProjectMemberId)]
    [InlineData(SameDepartmentTeamLeadId)]
    [InlineData(AdministratorId)]
    public async Task Owner_manager_member_same_department_lead_and_administrator_may_perform_every_operation(int userId)
    {
        var authorization = await BuildAuthorizationAsync();

        foreach (var operation in AllDocumentOperations)
            Assert.True((await authorization.AuthorizeAsync(userId, DocumentId, operation)).Allowed, $"{userId} should be allowed {operation}");
    }

    [Fact]
    public async Task Outsider_is_denied_every_operation_even_when_guessing_a_valid_document_id()
    {
        var authorization = await BuildAuthorizationAsync();

        foreach (var operation in AllDocumentOperations)
            Assert.False((await authorization.AuthorizeAsync(OutsiderId, DocumentId, operation)).Allowed, $"outsider should be denied {operation}");
    }

    [Theory]
    [InlineData(OwnerId)]
    [InlineData(ProjectManagerId)]
    [InlineData(ProjectMemberId)]
    [InlineData(SameDepartmentTeamLeadId)]
    [InlineData(AdministratorId)]
    [InlineData(OutsiderId)]
    [InlineData(RevokedShareUserId)]
    [InlineData(ActiveShareUserId)]
    public async Task Every_role_is_denied_every_operation_against_a_nonexistent_guessed_document_id(int userId)
    {
        var authorization = await BuildAuthorizationAsync();

        foreach (var operation in AllDocumentOperations)
            Assert.False((await authorization.AuthorizeAsync(userId, GuessedMissingDocumentId, operation)).Allowed);
        Assert.False((await authorization.AuthorizeAsync(userId, null, DocumentOperation.ReadMetadata)).Allowed);
    }

    [Fact]
    public async Task Revoked_share_user_is_denied_every_read_and_mutate_operation()
    {
        var authorization = await BuildAuthorizationAsync();

        foreach (var operation in AllDocumentOperations)
            Assert.False((await authorization.AuthorizeAsync(RevokedShareUserId, DocumentId, operation)).Allowed, $"revoked share should be denied {operation}");
    }

    [Fact]
    public async Task Active_share_user_may_only_perform_read_operations_not_mutations()
    {
        var authorization = await BuildAuthorizationAsync();

        foreach (var operation in ReadOperations)
            Assert.True((await authorization.AuthorizeAsync(ActiveShareUserId, DocumentId, operation)).Allowed, $"active share should allow {operation}");
        foreach (var operation in MutateOperations)
            Assert.False((await authorization.AuthorizeAsync(ActiveShareUserId, DocumentId, operation)).Allowed, $"active share should deny {operation}");
    }

    [Theory]
    [InlineData(AdministratorId, true)]
    [InlineData(OwnerId, false)]
    [InlineData(ProjectManagerId, false)]
    [InlineData(OutsiderId, false)]
    public async Task Audit_report_operation_is_administrator_only_regardless_of_document_ownership(int userId, bool expectedAllowed)
    {
        var authorization = await BuildAuthorizationAsync();

        var decision = await authorization.AuthorizeAsync(userId, null, DocumentOperation.AuditReport);

        Assert.Equal(expectedAllowed, decision.Allowed);
    }
}
