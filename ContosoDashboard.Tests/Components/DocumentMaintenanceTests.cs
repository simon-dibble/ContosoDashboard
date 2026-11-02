using Bunit;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ContosoDashboard.Tests.Components;

public class DocumentMaintenanceTests
{
    [Fact]
    public void Details_page_contains_maintenance_actions_and_does_not_render_storage_paths()
    {
        var page = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ContosoDashboard", "Pages", "DocumentDetails.razor"));
        Assert.Contains("Save metadata", page);
        Assert.Contains("Replace file", page);
        Assert.Contains("Share", page);
        Assert.Contains("Delete", page);
        Assert.Contains("DocumentService.UpdateMetadataAsync", page);
        Assert.Contains("DocumentService.ReplaceAsync", page);
        Assert.Contains("DocumentService.ShareAsync", page);
        Assert.Contains("DocumentService.DeleteAsync", page);
        Assert.DoesNotContain("StorageName", page);
        Assert.DoesNotContain("RootPath", page);
    }

    [Fact]
    public void Details_page_renders_owner_editable_metadata_for_an_authorized_document()
    {
        using var context = new TestContext();
        context.Services.AddSingleton<IDocumentService>(new StubDocumentService());

        var component = context.RenderComponent<ContosoDashboard.Pages.DocumentDetails>(
            parameters => parameters.Add(p => p.DocumentId, 1));

        Assert.Contains("US3 document", component.Markup);
        Assert.Contains("Save metadata", component.Markup);
        Assert.Contains("Replace file", component.Markup);
        Assert.DoesNotContain("private-storage-name", component.Markup);
    }

    [Fact]
    public void Shared_with_me_page_lists_only_what_the_service_authorizes()
    {
        using var context = new TestContext();
        context.Services.AddSingleton<IDocumentService>(new StubDocumentService());

        var component = context.RenderComponent<ContosoDashboard.Pages.SharedDocuments>();

        Assert.Contains("US3 document", component.Markup);
        Assert.Contains("Shared with me", component.Markup);
    }

    private sealed class StubDocumentService : IDocumentService
    {
        private static Document Doc() => new()
        {
            DocumentId = 1, Title = "US3 document", Category = "Reports", Description = "Metadata",
            OriginalFileName = "notes.txt", FileSize = 4, FileType = "text/plain",
            StorageName = "private-storage-name", UploadedAt = DateTime.UtcNow, ScanStatus = DocumentScanStatus.Passed
        };

        public Task<IReadOnlyList<DocumentUploadResult>> UploadAsync(string? title, string? description,
            string category, IEnumerable<string>? tags, int? projectId,
            IEnumerable<DocumentUploadRequest> files, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DocumentUploadResult>>(Array.Empty<DocumentUploadResult>());

        public Task<List<Document>> ListAsync(string? search = null, string? category = null,
            int? projectId = null, DateTime? from = null, DateTime? to = null, string sort = "date",
            CancellationToken cancellationToken = default) => Task.FromResult(new List<Document> { Doc() });

        public Task<Document?> GetAsync(int documentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Document?>(documentId == 1 ? Doc() : null);

        public Task<DocumentFileResult?> OpenReadAsync(int documentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<DocumentFileResult?>(null);

        public Task<Document?> UpdateMetadataAsync(int documentId, string title, string? description, string category,
            IEnumerable<string>? tags, CancellationToken cancellationToken = default) =>
            Task.FromResult<Document?>(Doc());

        public Task<DocumentOperationResult> ReplaceAsync(int documentId, DocumentUploadRequest file,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DocumentOperationResult(true));

        public Task<DocumentOperationResult> DeleteAsync(int documentId, bool confirmed,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DocumentOperationResult(confirmed));

        public Task<IReadOnlyList<DocumentShare>> ShareAsync(int documentId, DocumentShareRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DocumentShare>>(Array.Empty<DocumentShare>());

        public Task<bool> RevokeShareAsync(int documentId, int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<List<Document>> GetSharedWithMeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<Document> { Doc() });

        public Task<DocumentOperationResult> AttachToTaskAsync(int documentId, int taskId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DocumentOperationResult(true));

        public Task<List<Document>> GetTaskDocumentsAsync(int taskId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<Document>());
    }
}

