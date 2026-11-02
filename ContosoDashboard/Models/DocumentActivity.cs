using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models;

public class DocumentActivity
{
    [Key] public int DocumentActivityId { get; set; }
    public int? DocumentId { get; set; }
    [MaxLength(255)] public string? DocumentTitleSnapshot { get; set; }
    [MaxLength(255)] public string? FileTypeSnapshot { get; set; }
    public int? ProjectIdSnapshot { get; set; }
    [Required] public int ActorUserId { get; set; }
    [Required, MaxLength(30)] public string Action { get; set; } = string.Empty;
    [Required, MaxLength(30)] public string Outcome { get; set; } = string.Empty;
    [MaxLength(100)] public string? ReasonCode { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)] public string? CorrelationId { get; set; }
    [ForeignKey(nameof(DocumentId))] public Document? Document { get; set; }
    [ForeignKey(nameof(ActorUserId))] public User ActorUser { get; set; } = null!;
}
