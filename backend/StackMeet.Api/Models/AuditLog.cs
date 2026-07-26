namespace StackMeet.Api.Models;

/// <summary>
/// Records security- and competition-sensitive changes for later review.
/// </summary>
/// <remarks>
/// Phase 1 creates the table; later controller work should write entries when results,
/// assignments, competition settings or lifecycle state change.
/// </remarks>
public sealed class AuditLog
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    public AppUser? User { get; set; }
    public int? CompetitionId { get; set; }
    public Competition? Competition { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? OldValueJson { get; set; }
    public string? NewValueJson { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}
