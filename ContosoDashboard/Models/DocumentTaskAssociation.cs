using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models;

public class DocumentTaskAssociation
{
    [Key] public int DocumentTaskAssociationId { get; set; }
    public int DocumentId { get; set; }
    public int TaskId { get; set; }
    public int AttachedByUserId { get; set; }
    public DateTime AttachedAt { get; set; } = DateTime.UtcNow;
    [ForeignKey(nameof(DocumentId))] public Document Document { get; set; } = null!;
    [ForeignKey(nameof(TaskId))] public TaskItem Task { get; set; } = null!;
    [ForeignKey(nameof(AttachedByUserId))] public User AttachedByUser { get; set; } = null!;
}
