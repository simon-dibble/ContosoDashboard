using ContosoDashboard.Data;
using ContosoDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services;

public sealed record DocumentAuditSummary(string Action, string Outcome, int Count);
public sealed record DocumentFileTypeSummary(string FileType, int Count);
public sealed record DocumentUploaderSummary(int ActorUserId, string? ActorDisplayName, int Count);
public sealed record DocumentAuditReport(IReadOnlyList<DocumentActivity> Activities, IReadOnlyList<DocumentAuditSummary> Aggregates,
    IReadOnlyList<DocumentFileTypeSummary> MostUploadedFileTypes, IReadOnlyList<DocumentUploaderSummary> MostActiveUploaders);

public interface IDocumentAuditService
{
    Task RecordAsync(int actorUserId, string action, string outcome, Document? document = null, string? reasonCode = null, CancellationToken cancellationToken = default);
    Task<DocumentAuditReport?> GetReportAsync(int requestingUserId, int take = 200, CancellationToken cancellationToken = default);
}

public sealed class DocumentAuditService : IDocumentAuditService
{
    private readonly ApplicationDbContext _context;
    public DocumentAuditService(ApplicationDbContext context) => _context = context;

    public async Task RecordAsync(int actorUserId, string action, string outcome, Document? document = null,
        string? reasonCode = null, CancellationToken cancellationToken = default)
    {
        _context.DocumentActivities.Add(new DocumentActivity
        {
            DocumentId = document?.DocumentId,
            DocumentTitleSnapshot = document?.Title,
            FileTypeSnapshot = document?.FileType,
            ProjectIdSnapshot = document?.ProjectId,
            ActorUserId = actorUserId,
            Action = action.Length > 30 ? action[..30] : action,
            Outcome = outcome.Length > 30 ? outcome[..30] : outcome,
            ReasonCode = reasonCode is null ? null : reasonCode[..Math.Min(reasonCode.Length, 100)],
            OccurredAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<DocumentAuditReport?> GetReportAsync(int requestingUserId, int take = 200, CancellationToken cancellationToken = default)
    {
        if (!await _context.Users.AnyAsync(u => u.UserId == requestingUserId && u.Role == UserRole.Administrator, cancellationToken))
            return null;
        take = Math.Clamp(take, 1, 1000);
        var activities = await _context.DocumentActivities.AsNoTracking().Include(a => a.ActorUser)
            .OrderByDescending(a => a.OccurredAt).Take(take).ToListAsync(cancellationToken);
        var aggregates = (await _context.DocumentActivities.AsNoTracking()
            .GroupBy(a => new { a.Action, a.Outcome }).Select(g => new { g.Key.Action, g.Key.Outcome, Count = g.Count() })
            .ToListAsync(cancellationToken))
            .OrderByDescending(x => x.Count).Take(100)
            .Select(x => new DocumentAuditSummary(x.Action, x.Outcome, x.Count)).ToList();
        var fileTypes = (await _context.DocumentActivities.AsNoTracking()
            .Where(a => a.Action == "Upload" && a.Outcome == "Succeeded" && a.FileTypeSnapshot != null)
            .GroupBy(a => a.FileTypeSnapshot!).Select(g => new { FileType = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken))
            .OrderByDescending(x => x.Count).Take(20)
            .Select(x => new DocumentFileTypeSummary(x.FileType, x.Count)).ToList();
        var uploaders = (await _context.DocumentActivities.AsNoTracking().Include(a => a.ActorUser)
            .Where(a => a.Action == "Upload" && a.Outcome == "Succeeded")
            .GroupBy(a => new { a.ActorUserId, Name = a.ActorUser != null ? a.ActorUser.DisplayName : null })
            .Select(g => new { g.Key.ActorUserId, g.Key.Name, Count = g.Count() })
            .ToListAsync(cancellationToken))
            .OrderByDescending(x => x.Count).Take(20)
            .Select(x => new DocumentUploaderSummary(x.ActorUserId, x.Name, x.Count)).ToList();
        return new(activities, aggregates, fileTypes, uploaders);
    }
}


