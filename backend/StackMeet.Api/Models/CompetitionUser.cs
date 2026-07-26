namespace StackMeet.Api.Models;

/// <summary>
/// Assigns one user to one competition with one role.
/// </summary>
/// <remarks>
/// This is the core access-control join table for the multi-competition account model.
/// </remarks>
public sealed class CompetitionUser
{
    public int Id { get; set; }
    public int CompetitionId { get; set; }
    public Competition Competition { get; set; } = null!;
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public int RoleId { get; set; }
    public AppRole Role { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime AssignedAt { get; set; }
    public int? AssignedByUserId { get; set; }
    public AppUser? AssignedByUser { get; set; }
}
