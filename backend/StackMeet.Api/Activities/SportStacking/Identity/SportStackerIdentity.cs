using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StackMeet.Api.Activities.SportStacking.Identity;

/// <summary>
/// Represents one permanent Sport Stacking person in NADITrack across competitions.
/// A competition-scoped <c>Stacker</c> remains a historical registration snapshot and is linked separately.
/// </summary>
[Table("SportStackerIdentity", Schema = "dbo")]
[Index(nameof(NadiTrackId), IsUnique = true, Name = "UX_SportStackerIdentity_NadiTrackId")]
public sealed class SportStackerIdentity
{
    /// <summary>Internal persistence key. This is never the public athlete identifier.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>Permanent public NADITrack identifier, for example NDT-7K4M2PX.</summary>
    [Required]
    [MaxLength(11)]
    public string NadiTrackId { get; set; } = string.Empty;

    /// <summary>Optional external WSSA reference. NADITrack ID remains the authoritative NADITrack identity.</summary>
    [MaxLength(50)]
    public string? WssaId { get; set; }

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Gender { get; set; } = string.Empty;

    public DateOnly? BirthDate { get; set; }

    [Required]
    [MaxLength(100)]
    public string Country { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Club { get; set; }

    [MaxLength(100)]
    public string? Region { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    /// <summary>
    /// Public visibility is opt-in. Private fields such as birth date, email, and phone are never implied public by this flag.
    /// </summary>
    public bool IsPublicProfile { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
