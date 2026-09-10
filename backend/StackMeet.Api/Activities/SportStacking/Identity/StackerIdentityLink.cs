namespace StackMeet.Api.Activities.SportStacking.Identity;

/// <summary>
/// Links one permanent NADITrack Sport Stacker identity to one competition-scoped Stacker registration snapshot.
/// </summary>
public sealed class StackerIdentityLink
{
    public long Id { get; set; }
    public long SportStackerIdentityId { get; set; }
    public int StackerId { get; set; }

    /// <summary>Records how the association was established for audit and later review.</summary>
    public string MatchMethod { get; set; } = string.Empty;

    public DateTime LinkedAt { get; set; }
    public int? LinkedByUserId { get; set; }
}
