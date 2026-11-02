using Bunit;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ContosoDashboard.Tests.Components;

public class DocumentsUploadTests
{
    [Fact]
    public void Component_renders_authorized_metadata_without_storage_details()
    {
        using var context = new TestContext();
        context.Services.AddSingleton<IDocumentService>(new StubDocumentService());

        var component = context.RenderComponent<ContosoDashboard.Pages.Documents>();

        component.MarkupMatches(component.Markup);
        Assert.Contains("US1 document", component.Markup);
        Assert.Contains("Project Documents", component.Markup);
        Assert.Contains("text/plain", component.Markup);
        Assert.DoesNotContain("private-storage-name", component.Markup);
    }

    [Fact]
    public void Documents_page_is_present_and_route_protected()
    {
        var page = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ContosoDashboard", "Pages", "Documents.razor"));
        Assert.Contains("@page \"/documents\"", page);
        Assert.Contains("@attribute [Authorize]", page);
        Assert.Contains("Upload files", page);
    }

    [Fact]
    public void Documents_page_contains_per_file_feedback_and_metadata_fields()
    {
        var page = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ContosoDashboard", "Pages", "Documents.razor"));

        Assert.Contains("multiple", page);
        Assert.Contains("result.FileName", page);
        Assert.Contains("result.Succeeded", page);
        Assert.Contains("Description", page);
        Assert.Contains("Tags", page);
        Assert.Contains("Project", page);
        Assert.Contains("Search authorized documents", page);
        Assert.Contains("Uploading", page);
        Assert.Contains("role=\"progressbar\"", page);
        Assert.Contains("@onsubmit:preventDefault", page);
    }

    [Fact]
    public void Documents_page_does_not_render_storage_names_or_paths()
    {
        var page = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ContosoDashboard", "Pages", "Documents.razor"));

        Assert.DoesNotContain("StorageName", page);
        Assert.DoesNotContain("RootPath", page);
        Assert.Contains("mock-authenticated", page);
    }

    private sealed class StubDocumentService : IDocumentService
    {
        public Task<IReadOnlyList<DocumentUploadResult>> UploadAsync(string? title, string? description,
            string category, IEnumerable<string>? tags, int? projectId,
            IEnumerable<DocumentUploadRequest> files, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DocumentUploadResult>>(Array.Empty<DocumentUploadResult>());

        public Task<List<Document>> ListAsync(string? search = null, string? category = null,
            int? projectId = null, DateTime? from = null, DateTime? to = null, string sort = "date",
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<Document>
            {
                new()
                {
                    DocumentId = 1, Title = "US1 document", Category = "Project Documents",
                    Description = "Metadata", OriginalFileName = "notes.txt", FileSize = 4,
                    FileType = "text/plain", StorageName = "private-storage-name",
                    UploadedAt = DateTime.UtcNow, ScanStatus = DocumentScanStatus.Passed
                }
            });

        public Task<Document?> GetAsync(int documentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Document?>(null);

        public Task<DocumentFileResult?> OpenReadAsync(int documentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<DocumentFileResult?>(null);

        public Task<Document?> UpdateMetadataAsync(int documentId, string title, string? description, string category,
            IEnumerable<string>? tags, CancellationToken cancellationToken = default) =>
            Task.FromResult<Document?>(null);

        public Task<DocumentOperationResult> ReplaceAsync(int documentId, DocumentUploadRequest file,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DocumentOperationResult(false, "not implemented"));

        public Task<DocumentOperationResult> DeleteAsync(int documentId, bool confirmed,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DocumentOperationResult(false, "not implemented"));

        public Task<IReadOnlyList<DocumentShare>> ShareAsync(int documentId, DocumentShareRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DocumentShare>>(Array.Empty<DocumentShare>());

        public Task<bool> RevokeShareAsync(int documentId, int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<List<Document>> GetSharedWithMeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<Document>());

        public Task<DocumentOperationResult> AttachToTaskAsync(int documentId, int taskId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DocumentOperationResult(false, "not implemented"));

        public Task<List<Document>> GetTaskDocumentsAsync(int taskId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<Document>());
    }
}

