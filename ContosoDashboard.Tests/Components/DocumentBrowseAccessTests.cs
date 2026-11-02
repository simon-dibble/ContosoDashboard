using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Tests.Components;

public class DocumentBrowseAccessTests
{
    private static string Page(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ContosoDashboard", "Pages", name));

    [Fact]
    public void Browse_page_exposes_all_authorized_filters_and_safe_empty_state()
    {
        var page = Page("Documents.razor");
        Assert.Contains("Search authorized documents", page);
        Assert.Contains("All categories", page);
        Assert.Contains("fromDate", page);
        Assert.Contains("toDate", page);
        Assert.Contains("No matching documents.", page);
        Assert.Contains("DocumentService.ListAsync", page);
    }

    [Fact]
    public void Details_page_uses_service_for_guessed_ids_and_has_preview_fallback()
    {
        var page = Page("DocumentDetails.razor");
        Assert.Contains("@page \"/documents/{DocumentId:int}\"", page);
        Assert.Contains("DocumentService.GetAsync(DocumentId)", page);
        Assert.Contains("Preview is not available", page);
        Assert.Contains("/documents/{DocumentId}/download", page);
        Assert.Contains("The file is not available.", page);
    }

    [Fact]
    public async Task Guessed_document_id_is_not_disclosed_through_the_real_authorization_service()
    {
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.Users.AddRange(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" },
            new User { UserId = 2, Email = "other@test", DisplayName = "Other" });
        context.Documents.Add(new Document { DocumentId = 7, UploadedByUserId = 1, Title = "Private", Category = "Other",
            StorageName = "opaque-7", OriginalFileName = "p.txt", FileSize = 1, FileType = "text/plain", ScanStatus = DocumentScanStatus.Passed });
        await context.SaveChangesAsync();
        var authorization = new DocumentAuthorizationService(context, new HttpContextAccessor());

        Assert.False((await authorization.AuthorizeAsync(2, 7, DocumentOperation.Preview)).Allowed);
        Assert.False((await authorization.AuthorizeAsync(2, 404, DocumentOperation.Preview)).Allowed);
        Assert.True((await authorization.AuthorizeAsync(1, 7, DocumentOperation.Preview)).Allowed);
    }

    [Fact]
    public async Task Revoked_share_and_post_membership_removal_both_end_access_immediately()
    {
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.Users.AddRange(new User { UserId = 1, Email = "owner@test", DisplayName = "Owner" },
            new User { UserId = 2, Email = "shared@test", DisplayName = "Shared" },
            new User { UserId = 3, Email = "member@test", DisplayName = "Member" });
        context.Projects.Add(new Project { ProjectId = 50, Name = "Project", ProjectManagerId = 1 });
        var membership = new ProjectMember { ProjectMemberId = 1, ProjectId = 50, UserId = 3 };
        context.ProjectMembers.Add(membership);
        context.Documents.Add(new Document { DocumentId = 8, UploadedByUserId = 1, Title = "Shared doc", Category = "Other",
            StorageName = "opaque-8", OriginalFileName = "s.txt", FileSize = 1, FileType = "text/plain", ScanStatus = DocumentScanStatus.Passed });
        context.Documents.Add(new Document { DocumentId = 9, UploadedByUserId = 1, Title = "Project doc", Category = "Other",
            ProjectId = 50, StorageName = "opaque-9", OriginalFileName = "p.txt", FileSize = 1, FileType = "text/plain", ScanStatus = DocumentScanStatus.Passed });
        context.DocumentShares.Add(new DocumentShare { DocumentId = 8, UserId = 2, SharedByUserId = 1 });
        await context.SaveChangesAsync();
        var authorization = new DocumentAuthorizationService(context, new HttpContextAccessor());

        Assert.True((await authorization.AuthorizeAsync(2, 8, DocumentOperation.Download)).Allowed);
        Assert.True((await authorization.AuthorizeAsync(3, 9, DocumentOperation.ReadMetadata)).Allowed);

        context.DocumentShares.Single().RevokedAt = DateTime.UtcNow;
        context.ProjectMembers.Remove(membership);
        await context.SaveChangesAsync();

        Assert.False((await authorization.AuthorizeAsync(2, 8, DocumentOperation.Download)).Allowed);
        Assert.False((await authorization.AuthorizeAsync(3, 9, DocumentOperation.ReadMetadata)).Allowed);
    }
}
