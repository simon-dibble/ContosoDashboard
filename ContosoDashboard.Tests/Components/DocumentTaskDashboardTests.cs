namespace ContosoDashboard.Tests.Components;

public class DocumentTaskDashboardTests
{
    [Fact]
    public void Tasks_page_delegates_attachment_authorization_to_document_service()
    {
        var page = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ContosoDashboard", "Pages", "Tasks.razor"));
        Assert.Contains("@inject IDocumentService DocumentService", page);
        Assert.Contains("DocumentService.AttachToTaskAsync", page);
        Assert.Contains("The document could not be attached.", page);
    }

    [Fact]
    public void Tasks_page_offers_upload_and_displays_authorized_task_documents()
    {
        var page = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ContosoDashboard", "Pages", "Tasks.razor"));
        Assert.Contains("<InputFile", page);
        Assert.Contains("UploadTaskDocumentAsync", page);
        Assert.Contains("DocumentService.UploadAsync", page);
        Assert.Contains("DocumentService.GetTaskDocumentsAsync", page);
        Assert.Contains("taskDocuments", page);
    }

    [Fact]
    public void Dashboard_page_renders_recent_authorized_documents_and_count()
    {
        var page = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ContosoDashboard", "Pages", "Index.razor"));
        Assert.Contains("GetRecentDocumentsAsync(currentUserId, 5)", page);
        Assert.Contains("summary.DocumentCount", page);
        Assert.Contains("/documents/{document.DocumentId}", page);
    }
}

