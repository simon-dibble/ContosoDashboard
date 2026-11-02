using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models;

public class DocumentShare
{
    [Key] public int DocumentShareId { get; set; }
    public int DocumentId { get; set; }
    public int UserId { get; set; }
    [MaxLength(100)] public string? SourceTeam { get; set; }
    public int SharedByUserId { get; set; }
    public DateTime SharedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    [ForeignKey(nameof(DocumentId))] public Document Document { get; set; } = null!;
    [ForeignKey(nameof(UserId))] public User User { get; set; } = null!;
    [ForeignKey(nameof(SharedByUserId))] public User SharedByUser { get; set; } = null!;
}
