using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ContosoDashboard.Tests.Services;

internal sealed class DocumentTestAuthorization(ApplicationDbContext context, int userId) : IDocumentAuthorizationService
{
    public int UserId { get; set; } = userId;
    public Task<int?> GetCurrentUserIdAsync(CancellationToken cancellationToken = default) => Task.FromResult<int?>(UserId);

    public IQueryable<Document> AuthorizedDocuments(int userId, DocumentOperation operation = DocumentOperation.ReadMetadata)
    {
        var readable = operation is DocumentOperation.ReadMetadata or DocumentOperation.Search or
            DocumentOperation.Preview or DocumentOperation.Download or DocumentOperation.Attach;
        return context.Documents.Where(d => !d.IsDeleted && d.ScanStatus == DocumentScanStatus.Passed &&
            (d.UploadedByUserId == userId ||
             (d.ProjectId.HasValue && context.ProjectMembers.Any(m => m.ProjectId == d.ProjectId && m.UserId == userId)) ||
             context.Projects.Any(p => p.ProjectId == d.ProjectId && p.ProjectManagerId == userId) ||
             (readable && context.DocumentShares.Any(s => s.DocumentId == d.DocumentId && s.UserId == userId && s.RevokedAt == null))));
    }

    public async Task<DocumentAuthorizationDecision> AuthorizeAsync(int userId, int? documentId,
        DocumentOperation operation, CancellationToken cancellationToken = default) =>
        new(documentId.HasValue && await AuthorizedDocuments(userId, operation)
            .AnyAsync(d => d.DocumentId == documentId.Value, cancellationToken));
}

internal sealed class MemoryDocumentStorage : IFileStorage
{
    private readonly Dictionary<string, byte[]> _files = new();
    public int OpenCount { get; private set; }
    public Task<TemporaryStorageHandle> CreateTemporaryAsync(Stream content, CancellationToken cancellationToken = default)
    {
        using var copy = new MemoryStream();
        content.CopyTo(copy);
        var name = "tmp-" + Guid.NewGuid().ToString("N");
        _files[name] = copy.ToArray();
        return Task.FromResult(new TemporaryStorageHandle(name));
    }
    public Task<CommittedStorageHandle> CommitAsync(TemporaryStorageHandle temporaryHandle, string opaqueStorageName, CancellationToken cancellationToken = default)
    {
        _files[opaqueStorageName] = _files[temporaryHandle.Name];
        _files.Remove(temporaryHandle.Name);
        return Task.FromResult(new CommittedStorageHandle(opaqueStorageName));
    }
    public Task<Stream> OpenReadAsync(string opaqueStorageName, CancellationToken cancellationToken = default)
    {
        OpenCount++;
        return Task.FromResult<Stream>(new MemoryStream(_files[opaqueStorageName]));
    }
    public Task<bool> DeleteAsync(string opaqueStorageName, CancellationToken cancellationToken = default)
    {
        _files.Remove(opaqueStorageName);
        return Task.FromResult(true);
    }
}

internal static class DocumentTestData
{
    public static ApplicationDbContext Context() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    public static DocumentService Service(ApplicationDbContext context, DocumentTestAuthorization authorization,
        MemoryDocumentStorage? storage = null) =>
        new(context, storage ?? new MemoryDocumentStorage(), new DeterministicMalwareScanner(),
            authorization, new DocumentValidationService(), new NotificationService(context),
            NullLogger<DocumentService>.Instance, new DocumentAuditService(context));

    public static Document Document(int id, int owner, string title, DateTime uploadedAt, int? project = null) =>
        new() { DocumentId = id, UploadedByUserId = owner, Title = title, Category = "Reports",
            Tags = "alpha beta", Description = "description", ProjectId = project, StorageName = $"opaque-{id}",
            OriginalFileName = $"{title}.txt", FileSize = 3, FileType = "text/plain",
            UploadedAt = uploadedAt, ScanStatus = DocumentScanStatus.Passed };
}

