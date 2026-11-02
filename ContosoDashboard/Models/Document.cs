using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models;

public enum DocumentScanStatus { Pending, Passed, Rejected }

public static class DocumentCategories
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "Project Documents", "Team Resources", "Personal Files", "Reports", "Presentations", "Other"
    };
}

public class Document
{
    [Key] public int DocumentId { get; set; }
    [Required, MaxLength(255)] public string Title { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Description { get; set; }
    [Required, MaxLength(50)] public string Category { get; set; } = string.Empty;
    [MaxLength(2000)] public string Tags { get; set; } = string.Empty;
    public int? ProjectId { get; set; }
    [Required] public int UploadedByUserId { get; set; }
    public int? TaskId { get; set; }
    [Required, MaxLength(255)] public string StorageName { get; set; } = string.Empty;
    [Required, MaxLength(255)] public string OriginalFileName { get; set; } = string.Empty;
    [Range(1, 26214400)] public long FileSize { get; set; }
    [Required, MaxLength(255)] public string FileType { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DocumentScanStatus ScanStatus { get; set; } = DocumentScanStatus.Pending;
    public bool IsDeleted { get; set; }
    [ForeignKey(nameof(ProjectId))] public virtual Project? Project { get; set; }
    [ForeignKey(nameof(UploadedByUserId))] public virtual User UploadedByUser { get; set; } = null!;
    public virtual ICollection<DocumentShare> Shares { get; set; } = new List<DocumentShare>();
    public virtual ICollection<DocumentTaskAssociation> TaskAssociations { get; set; } = new List<DocumentTaskAssociation>();
    public virtual ICollection<DocumentActivity> Activities { get; set; } = new List<DocumentActivity>();
}
