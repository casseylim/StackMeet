namespace StackMeet.Api.Activities.SportStacking.Identity;

/// <summary>
/// Represents one permanent Sport Stacking person in NADITrack across competitions.
/// A competition-scoped <c>Stacker</c> remains a historical registration snapshot and is linked separately.
/// </summary>
public sealed class SportStackerIdentity
{
    /// <summary>Internal persistence key. This is never the public athlete identifier.</summary>
    public long Id { get; set; }

    /// <summary>Permanent public NADITrack identifier, for example NDT-7K4M2PX.</summary>
    public string NadiTrackId { get; set; } = string.Empty;

    /// <summary>Optional external WSSA reference. NADITrack ID remains the authoritative NADITrack identity.</summary>
    public string? WssaId { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateOnly? BirthDate { get; set; }
    public string Country { get; set; } = string.Empty;
    public string? Club { get; set; }
    public string? Region { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }

    /// <summary>
    /// Public visibility is opt-in. Private fields such as birth date, email, and phone are never implied public by this flag.
    /// </summary>
    public bool IsPublicProfile { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
