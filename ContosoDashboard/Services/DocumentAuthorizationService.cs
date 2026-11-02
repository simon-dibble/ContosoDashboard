using System.Security.Claims;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services;

public enum DocumentOperation { ReadMetadata, Search, Preview, Download, Edit, Replace, Share, Delete, Attach, AuditReport }
public sealed record DocumentAuthorizationDecision(bool Allowed);

public interface IDocumentAuthorizationService
{
    Task<int?> GetCurrentUserIdAsync(CancellationToken cancellationToken = default);
    IQueryable<Document> AuthorizedDocuments(int userId, DocumentOperation operation = DocumentOperation.ReadMetadata);
    Task<DocumentAuthorizationDecision> AuthorizeAsync(int userId, int? documentId, DocumentOperation operation, CancellationToken cancellationToken = default);
}

public sealed class DocumentAuthorizationService : IDocumentAuthorizationService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    public DocumentAuthorizationService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    { _context = context; _httpContextAccessor = httpContextAccessor; }

    public Task<int?> GetCurrentUserIdAsync(CancellationToken cancellationToken = default)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            return Task.FromResult<int?>(null);
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Task.FromResult(int.TryParse(value, out var id) ? (int?)id : null);
    }

    public IQueryable<Document> AuthorizedDocuments(int userId, DocumentOperation operation = DocumentOperation.ReadMetadata)
    {
        var currentUser = _context.Users.Where(u => u.UserId == userId);
        var isAdministrator = currentUser.Any(u => u.Role == UserRole.Administrator);
        var isOwnerOrProjectUser = _context.Documents.Where(d =>
            isAdministrator ||
            d.UploadedByUserId == userId ||
            (d.ProjectId.HasValue &&
                (_context.Projects.Any(p => p.ProjectId == d.ProjectId && p.ProjectManagerId == userId) ||
                 _context.ProjectMembers.Any(pm => pm.ProjectId == d.ProjectId && pm.UserId == userId))) ||
            (currentUser.Any(u => u.Role == UserRole.TeamLead) &&
             _context.Users.Any(owner => owner.UserId == d.UploadedByUserId &&
                 owner.Department != null &&
                 currentUser.Any(u => u.Department == owner.Department))));

        var readOperation = operation is DocumentOperation.ReadMetadata or DocumentOperation.Search
            or DocumentOperation.Preview or DocumentOperation.Download or DocumentOperation.Attach;

        return _context.Documents.Where(d => !d.IsDeleted && d.ScanStatus == DocumentScanStatus.Passed)
            .Where(d => isOwnerOrProjectUser.Any(allowed => allowed.DocumentId == d.DocumentId) ||
                (readOperation &&
                    (_context.DocumentShares.Any(s => s.DocumentId == d.DocumentId &&
                        s.UserId == userId && s.RevokedAt == null) ||
                     _context.DocumentTaskAssociations.Any(a => a.DocumentId == d.DocumentId &&
                        (a.Task.AssignedUserId == userId || a.Task.CreatedByUserId == userId ||
                         (a.Task.ProjectId.HasValue &&
                            (_context.Projects.Any(p => p.ProjectId == a.Task.ProjectId && p.ProjectManagerId == userId) ||
                             _context.ProjectMembers.Any(pm => pm.ProjectId == a.Task.ProjectId && pm.UserId == userId))))))));
    }

    public async Task<DocumentAuthorizationDecision> AuthorizeAsync(int userId, int? documentId, DocumentOperation operation, CancellationToken cancellationToken = default)
    {
        if (operation == DocumentOperation.AuditReport)
            return new(await _context.Users.AnyAsync(u => u.UserId == userId && u.Role == UserRole.Administrator, cancellationToken));
        if (!documentId.HasValue) return new(false);
        return new(await AuthorizedDocuments(userId, operation).AnyAsync(d => d.DocumentId == documentId.Value, cancellationToken));
    }
}
