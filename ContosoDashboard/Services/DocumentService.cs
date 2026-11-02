using System.Security.Cryptography;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services;

public sealed record DocumentUploadRequest(string FileName, string ContentType, Stream Content, long Length);
public sealed record DocumentUploadResult(string FileName, bool Succeeded, string? Error, Document? Document);
public sealed record DocumentFileResult(Document Document, Stream Content);
public sealed record DocumentShareRequest(IEnumerable<int> UserIds, string? SourceTeam = null);
public sealed record DocumentOperationResult(bool Succeeded, string? Error = null);

public interface IDocumentService
{
    Task<IReadOnlyList<DocumentUploadResult>> UploadAsync(string? title, string? description, string category, IEnumerable<string>? tags, int? projectId, IEnumerable<DocumentUploadRequest> files, CancellationToken cancellationToken = default);
    Task<List<Document>> ListAsync(string? search = null, string? category = null, int? projectId = null, DateTime? from = null, DateTime? to = null, string sort = "date", CancellationToken cancellationToken = default);
    Task<Document?> GetAsync(int documentId, CancellationToken cancellationToken = default);
    Task<DocumentFileResult?> OpenReadAsync(int documentId, CancellationToken cancellationToken = default);
    Task<Document?> UpdateMetadataAsync(int documentId, string title, string? description, string category, IEnumerable<string>? tags, CancellationToken cancellationToken = default);
    Task<DocumentOperationResult> ReplaceAsync(int documentId, DocumentUploadRequest file, CancellationToken cancellationToken = default);
    Task<DocumentOperationResult> DeleteAsync(int documentId, bool confirmed, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentShare>> ShareAsync(int documentId, DocumentShareRequest request, CancellationToken cancellationToken = default);
    Task<bool> RevokeShareAsync(int documentId, int userId, CancellationToken cancellationToken = default);
    Task<List<Document>> GetSharedWithMeAsync(CancellationToken cancellationToken = default);
    Task<DocumentOperationResult> AttachToTaskAsync(int documentId, int taskId, CancellationToken cancellationToken = default);
    Task<List<Document>> GetTaskDocumentsAsync(int taskId, CancellationToken cancellationToken = default);
}

public sealed class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorage _storage;
    private readonly IMalwareScanner _scanner;
    private readonly IDocumentAuthorizationService _authorization;
    private readonly IDocumentValidationService _validation;
    private readonly INotificationService _notificationService;
    private readonly IDocumentAuditService? _audit;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(ApplicationDbContext context, IFileStorage storage, IMalwareScanner scanner,
        IDocumentAuthorizationService authorization, IDocumentValidationService validation, INotificationService notificationService,
        ILogger<DocumentService> logger, IDocumentAuditService? audit = null)
    { _context = context; _storage = storage; _scanner = scanner; _authorization = authorization; _validation = validation; _notificationService = notificationService; _logger = logger; _audit = audit; }

    public DocumentService(ApplicationDbContext context, IFileStorage storage, IMalwareScanner scanner,
        IDocumentAuthorizationService authorization, IDocumentValidationService validation, ILogger<DocumentService> logger)
        : this(context, storage, scanner, authorization, validation, new NotificationService(context), logger, null) { }

    public async Task<IReadOnlyList<DocumentUploadResult>> UploadAsync(string? title, string? description, string category,
        IEnumerable<string>? tags, int? projectId, IEnumerable<DocumentUploadRequest> files, CancellationToken cancellationToken = default)
    {
        var userId = await _authorization.GetCurrentUserIdAsync(cancellationToken);
        if (!userId.HasValue) return files.Select(f => new DocumentUploadResult(f.FileName, false, "Authentication is required.", null)).ToList();
        var metadata = _validation.ValidateMetadata(title, description, category, tags);
        if (!metadata.IsValid) return files.Select(f => new DocumentUploadResult(f.FileName, false, metadata.Error, null)).ToList();
        if (!await CanUseProjectAsync(userId.Value, projectId, cancellationToken))
            return files.Select(f => new DocumentUploadResult(f.FileName, false, "The selected project is not available.", null)).ToList();

        var results = new List<DocumentUploadResult>();
        foreach (var file in files)
            results.Add(await UploadOneAsync(userId.Value, metadata, projectId, file, cancellationToken));
        return results;
    }

    private async Task<DocumentUploadResult> UploadOneAsync(int userId, DocumentValidationResult metadata, int? projectId,
        DocumentUploadRequest file, CancellationToken cancellationToken)
    {
        TemporaryStorageHandle? temporary = null;
        string? committedName = null;
        var saved = false;
        try
        {
            if (!_validation.IsAllowedFile(file.FileName, file.ContentType, file.Length, out var error))
                return new(file.FileName, false, error, null);

            // Browser streams are commonly non-seekable. Buffer once with an enforced upper
            // bound so storage and scanning always inspect the exact same bytes.
            await using var buffered = new MemoryStream(file.Length <= int.MaxValue ? (int)file.Length : 0);
            var copyBuffer = new byte[64 * 1024];
            long actualLength = 0;
            while (true)
            {
                var read = await file.Content.ReadAsync(copyBuffer, cancellationToken);
                if (read == 0) break;
                actualLength += read;
                if (actualLength > 25 * 1024 * 1024)
                    return new(file.FileName, false, "Each file must be between 1 byte and 25 MiB.", null);
                await buffered.WriteAsync(copyBuffer.AsMemory(0, read), cancellationToken);
            }
            if (actualLength != file.Length)
                return new(file.FileName, false, "The file size could not be verified.", null);

            buffered.Position = 0;
            var scan = await _scanner.ScanAsync(buffered, Path.GetFileName(file.FileName), file.ContentType, cancellationToken);
            if (scan.Outcome != MalwareScanOutcome.Passed)
                return new(file.FileName, false, scan.Outcome == MalwareScanOutcome.Rejected ? "The file was rejected by the training malware scan." : "The file could not be scanned.", null);

            buffered.Position = 0;
            temporary = await _storage.CreateTemporaryAsync(buffered, cancellationToken);
            var storageName = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            var committed = await _storage.CommitAsync(temporary, storageName, cancellationToken);
            committedName = committed.Name;
            var document = new Document
            {
                Title = metadata.Title, Description = metadata.Description, Category = metadata.Category,
                Tags = metadata.Tags, ProjectId = projectId, UploadedByUserId = userId, StorageName = committed.Name,
                OriginalFileName = Path.GetFileName(file.FileName), FileSize = file.Length, FileType = file.ContentType,
                UploadedAt = DateTime.UtcNow, ScanStatus = DocumentScanStatus.Passed
            };
            _context.Documents.Add(document);
            await _context.SaveChangesAsync(cancellationToken);
            if (_audit is not null)
                await _audit.RecordAsync(userId, "Upload", "Succeeded", document, cancellationToken: cancellationToken);
            if (projectId.HasValue)
                await NotifyProjectMembersOfNewDocumentAsync(projectId.Value, userId, document, cancellationToken);
            saved = true;
            return new(file.FileName, true, null, document);
        }
        catch (Exception ex) when (ex is IOException or DbUpdateException or UnauthorizedAccessException)
        {
            if (committedName is not null)
            {
                foreach (var entry in _context.ChangeTracker.Entries<Document>()
                    .Where(entry => entry.State == EntityState.Added &&
                        entry.Entity.StorageName == committedName))
                    entry.State = EntityState.Detached;
            }
            _logger.LogWarning("Document upload failed with a recoverable storage or metadata error.");
            return new(file.FileName, false, "The document could not be stored.", null);
        }
        finally
        {
            if (temporary is not null && committedName is null)
                await TryDeleteAsync(temporary.Name, CancellationToken.None);
            if (committedName is not null && !saved)
                await TryDeleteAsync(committedName, CancellationToken.None);
        }
    }

    public async Task<List<Document>> ListAsync(string? search = null, string? category = null, int? projectId = null,
        DateTime? from = null, DateTime? to = null, string sort = "date", CancellationToken cancellationToken = default)
    {
        var userId = await _authorization.GetCurrentUserIdAsync(cancellationToken);
        if (!userId.HasValue || !_validation.IsValidDateRange(from, to)) return new();
        var query = _authorization.AuthorizedDocuments(userId.Value).Include(d => d.Project).Include(d => d.UploadedByUser).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(d => d.Title.Contains(term) || (d.Description != null && d.Description.Contains(term)) ||
                d.Tags.Contains(term) || d.OriginalFileName.Contains(term) ||
                d.UploadedByUser.DisplayName.Contains(term) || (d.Project != null && d.Project.Name.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(d => d.Category == category);
        if (projectId.HasValue) query = query.Where(d => d.ProjectId == projectId);
        if (from.HasValue) query = query.Where(d => d.UploadedAt >= from.Value.ToUniversalTime());
        if (to.HasValue) query = query.Where(d => d.UploadedAt < to.Value.Date.AddDays(1).ToUniversalTime());
        query = sort.ToLowerInvariant() switch
        {
            "title" => query.OrderBy(d => d.Title).ThenBy(d => d.DocumentId),
            "category" => query.OrderBy(d => d.Category).ThenBy(d => d.DocumentId),
            "size" => query.OrderBy(d => d.FileSize).ThenBy(d => d.DocumentId),
            _ => query.OrderByDescending(d => d.UploadedAt).ThenByDescending(d => d.DocumentId)
        };
        return await query.Take(500).ToListAsync(cancellationToken);
    }

    public async Task<Document?> GetAsync(int documentId, CancellationToken cancellationToken = default)
    {
        var userId = await _authorization.GetCurrentUserIdAsync(cancellationToken);
        if (!userId.HasValue) return null;
        return await _authorization.AuthorizedDocuments(userId.Value).Include(d => d.Project).Include(d => d.UploadedByUser)
            .AsNoTracking().SingleOrDefaultAsync(d => d.DocumentId == documentId, cancellationToken);
    }

    public async Task<DocumentFileResult?> OpenReadAsync(int documentId, CancellationToken cancellationToken = default)
    {
        var document = await GetAsync(documentId, cancellationToken);
        if (document is null) return null;
        try
        {
            var stream = await _storage.OpenReadAsync(document.StorageName, cancellationToken);
            if (_audit is not null)
                if (await _authorization.GetCurrentUserIdAsync(cancellationToken) is int actor)
                    await _audit.RecordAsync(actor, "Download", "Succeeded", document, cancellationToken: cancellationToken);
            return new(document, stream);
        }
        catch (FileNotFoundException) { return null; }
        catch (ArgumentException) { return null; }
    }

    public async Task<Document?> UpdateMetadataAsync(int documentId, string title, string? description, string category,
        IEnumerable<string>? tags, CancellationToken cancellationToken = default)
    {
        var actor = await _authorization.GetCurrentUserIdAsync(cancellationToken);
        if (!actor.HasValue) return null;
        var decision = await _authorization.AuthorizeAsync(actor.Value, documentId, DocumentOperation.Edit, cancellationToken);
        if (!decision.Allowed) return null;
        var metadata = _validation.ValidateMetadata(title, description, category, tags);
        if (!metadata.IsValid) return null;
        var document = await _context.Documents.SingleOrDefaultAsync(d => d.DocumentId == documentId && !d.IsDeleted, cancellationToken);
        if (document is null) return null;
        document.Title = metadata.Title;
        document.Description = metadata.Description;
        document.Category = metadata.Category;
        document.Tags = metadata.Tags;
        await _context.SaveChangesAsync(cancellationToken);
        return document;
    }

    public async Task<DocumentOperationResult> ReplaceAsync(int documentId, DocumentUploadRequest file, CancellationToken cancellationToken = default)
    {
        var actor = await _authorization.GetCurrentUserIdAsync(cancellationToken);
        if (!actor.HasValue || !(await _authorization.AuthorizeAsync(actor.Value, documentId, DocumentOperation.Replace, cancellationToken)).Allowed)
            return new(false, "The document is not available.");
        var document = await _context.Documents.SingleOrDefaultAsync(d => d.DocumentId == documentId && !d.IsDeleted, cancellationToken);
        if (document is null) return new(false, "The document is not available.");
        if (!_validation.IsAllowedFile(file.FileName, file.ContentType, file.Length, out var error))
            return new(false, error);
        await using var buffer = new MemoryStream();
        await file.Content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length != file.Length || buffer.Length > 25 * 1024 * 1024)
            return new(false, "The file size could not be verified.");
        buffer.Position = 0;
        var scan = await _scanner.ScanAsync(buffer, Path.GetFileName(file.FileName), file.ContentType, cancellationToken);
        if (scan.Outcome != MalwareScanOutcome.Passed) return new(false, "The replacement could not be accepted.");
        TemporaryStorageHandle? temporary = null;
        string? committed = null;
        try
        {
            buffer.Position = 0;
            temporary = await _storage.CreateTemporaryAsync(buffer, cancellationToken);
            committed = (await _storage.CommitAsync(temporary, Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant(), cancellationToken)).Name;
            var oldName = document.StorageName;
            document.StorageName = committed;
            document.OriginalFileName = Path.GetFileName(file.FileName);
            document.FileSize = buffer.Length;
            document.FileType = file.ContentType;
            document.UploadedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            await TryDeleteAsync(oldName, cancellationToken);
            committed = null;
            return new(true);
        }
        catch (Exception ex) when (ex is IOException or DbUpdateException or UnauthorizedAccessException)
        {
            return new(false, "The replacement could not be stored.");
        }
        finally
        {
            if (temporary is not null && committed is null) { /* committed content is now owned by the document */ }
            if (committed is not null) await TryDeleteAsync(committed, CancellationToken.None);
        }
    }

    public async Task<DocumentOperationResult> DeleteAsync(int documentId, bool confirmed, CancellationToken cancellationToken = default)
    {
        if (!confirmed) return new(false, "Confirmation is required.");
        var actor = await _authorization.GetCurrentUserIdAsync(cancellationToken);
        if (!actor.HasValue || !(await _authorization.AuthorizeAsync(actor.Value, documentId, DocumentOperation.Delete, cancellationToken)).Allowed)
            return new(false, "The document is not available.");
        var document = await _context.Documents.SingleOrDefaultAsync(d => d.DocumentId == documentId && !d.IsDeleted, cancellationToken);
        if (document is null) return new(false, "The document is not available.");
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        _context.DocumentActivities.Add(new DocumentActivity { DocumentId = null, DocumentTitleSnapshot = document.Title,
            FileTypeSnapshot = document.FileType, ProjectIdSnapshot = document.ProjectId, ActorUserId = actor.Value,
            Action = "Delete", Outcome = "Succeeded", OccurredAt = DateTime.UtcNow });
        var storageName = document.StorageName;
        _context.Documents.Remove(document);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await TryDeleteAsync(storageName, cancellationToken);
        return new(true);
    }

    public async Task<IReadOnlyList<DocumentShare>> ShareAsync(int documentId, DocumentShareRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await _authorization.GetCurrentUserIdAsync(cancellationToken);
        if (!actor.HasValue || !(await _authorization.AuthorizeAsync(actor.Value, documentId, DocumentOperation.Share, cancellationToken)).Allowed)
            return Array.Empty<DocumentShare>();
        var document = await _context.Documents.SingleOrDefaultAsync(d => d.DocumentId == documentId && !d.IsDeleted, cancellationToken);
        if (document is null) return Array.Empty<DocumentShare>();
        var ids = request.UserIds.Distinct().Where(id => id != actor.Value).ToList();
        var existing = await _context.DocumentShares.Where(s => s.DocumentId == documentId && ids.Contains(s.UserId) && s.RevokedAt == null).ToListAsync(cancellationToken);
        var shares = new List<DocumentShare>();
        foreach (var id in ids.Except(existing.Select(s => s.UserId)))
        {
            if (!await _context.Users.AnyAsync(u => u.UserId == id, cancellationToken)) continue;
            var share = new DocumentShare { DocumentId = documentId, UserId = id, SharedByUserId = actor.Value, SourceTeam = request.SourceTeam, SharedAt = DateTime.UtcNow };
            _context.DocumentShares.Add(share); shares.Add(share);
        }
        await _context.SaveChangesAsync(cancellationToken);
        foreach (var share in shares)
            await _notificationService.CreateNotificationAsync(new Notification { UserId = share.UserId, Title = "Document shared with you",
                Message = $"A document has been shared with you: {document.Title}", Type = NotificationType.DocumentShared, Priority = NotificationPriority.Informational });
        if (_audit is not null && shares.Count > 0)
            await _audit.RecordAsync(actor.Value, "Share", "Succeeded", document, cancellationToken: cancellationToken);
        return existing.Concat(shares).ToList();
    }

    public async Task<bool> RevokeShareAsync(int documentId, int userId, CancellationToken cancellationToken = default)
    {
        var actor = await _authorization.GetCurrentUserIdAsync(cancellationToken);
        if (!actor.HasValue || !(await _authorization.AuthorizeAsync(actor.Value, documentId, DocumentOperation.Share, cancellationToken)).Allowed) return false;
        var share = await _context.DocumentShares.SingleOrDefaultAsync(s => s.DocumentId == documentId && s.UserId == userId && s.RevokedAt == null, cancellationToken);
        if (share is null) return false;
        share.RevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<Document>> GetSharedWithMeAsync(CancellationToken cancellationToken = default)
    {
        var actor = await _authorization.GetCurrentUserIdAsync(cancellationToken);
        if (!actor.HasValue) return new();
        return await _context.Documents.Include(d => d.Project)
            .Where(d => !d.IsDeleted && d.ScanStatus == DocumentScanStatus.Passed &&
                _context.DocumentShares.Any(s => s.DocumentId == d.DocumentId && s.UserId == actor.Value && s.RevokedAt == null))
            .OrderByDescending(d => d.UploadedAt).Take(500).AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<DocumentOperationResult> AttachToTaskAsync(int documentId, int taskId, CancellationToken cancellationToken = default)
    {
        var actor = await _authorization.GetCurrentUserIdAsync(cancellationToken);
        if (!actor.HasValue || !(await _authorization.AuthorizeAsync(actor.Value, documentId, DocumentOperation.Attach, cancellationToken)).Allowed)
            return new(false, "The document is not available.");
        var task = await _context.Tasks.Include(t => t.Project).ThenInclude(p => p.ProjectMembers).SingleOrDefaultAsync(t => t.TaskId == taskId, cancellationToken);
        var document = await _context.Documents.SingleOrDefaultAsync(d => d.DocumentId == documentId && !d.IsDeleted, cancellationToken);
        if (task is null || document is null || document.ProjectId != task.ProjectId) return new(false, "The document and task must belong to the same project.");
        var allowed = task.AssignedUserId == actor.Value || task.CreatedByUserId == actor.Value ||
            task.Project?.ProjectManagerId == actor.Value || task.Project?.ProjectMembers.Any(m => m.UserId == actor.Value) == true;
        if (!allowed) return new(false, "The task is not available.");
        if (!await _context.DocumentTaskAssociations.AnyAsync(a => a.DocumentId == documentId && a.TaskId == taskId, cancellationToken))
            _context.DocumentTaskAssociations.Add(new DocumentTaskAssociation { DocumentId = documentId, TaskId = taskId, AttachedByUserId = actor.Value });
        await _context.SaveChangesAsync(cancellationToken);
        return new(true);
    }

    public async Task<List<Document>> GetTaskDocumentsAsync(int taskId, CancellationToken cancellationToken = default)
    {
        var actor = await _authorization.GetCurrentUserIdAsync(cancellationToken);
        if (!actor.HasValue) return new();
        var authorized = _authorization.AuthorizedDocuments(actor.Value, DocumentOperation.Attach);
        return await authorized.Where(d => _context.DocumentTaskAssociations.Any(a => a.TaskId == taskId && a.DocumentId == d.DocumentId))
            .OrderByDescending(d => d.UploadedAt).AsNoTracking().ToListAsync(cancellationToken);
    }

    private async Task<bool> CanUseProjectAsync(int userId, int? projectId, CancellationToken cancellationToken)
    {
        if (!projectId.HasValue) return true;
        return await _context.Users.AnyAsync(u => u.UserId == userId && u.Role == UserRole.Administrator, cancellationToken) ||
            await _context.Projects.AnyAsync(p => p.ProjectId == projectId && p.ProjectManagerId == userId, cancellationToken) ||
            await _context.ProjectMembers.AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId, cancellationToken);
    }

    private async Task NotifyProjectMembersOfNewDocumentAsync(int projectId, int uploaderUserId, Document document, CancellationToken cancellationToken)
    {
        var recipients = await _context.ProjectMembers.Where(pm => pm.ProjectId == projectId && pm.UserId != uploaderUserId)
            .Select(pm => pm.UserId).ToListAsync(cancellationToken);
        var managerId = await _context.Projects.Where(p => p.ProjectId == projectId && p.ProjectManagerId != uploaderUserId)
            .Select(p => p.ProjectManagerId).SingleOrDefaultAsync(cancellationToken);
        foreach (var recipientId in recipients.Append(managerId).Where(id => id != 0).Distinct())
            await _notificationService.CreateNotificationAsync(new Notification { UserId = recipientId, Title = "New project document",
                Message = $"A document was added to your project: {document.Title}", Type = NotificationType.ProjectUpdate, Priority = NotificationPriority.Informational });
    }

    private async Task TryDeleteAsync(string name, CancellationToken cancellationToken)
    {
        try { await _storage.DeleteAsync(name, cancellationToken); } catch { /* cleanup is best effort and never discloses paths */ }
    }
}





