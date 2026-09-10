using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Models;

namespace StackMeet.Api.Activities.SportStacking.Identity;

/// <summary>
/// Links one permanent NADITrack Sport Stacker identity to one competition-scoped Stacker registration snapshot.
/// </summary>
[Table("StackerIdentityLink", Schema = "dbo")]
[Index(nameof(StackerId), IsUnique = true, Name = "UX_StackerIdentityLink_StackerId")]
[Index(nameof(SportStackerIdentityId), Name = "IX_StackerIdentityLink_SportStackerIdentityId")]
public sealed class StackerIdentityLink
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public long SportStackerIdentityId { get; set; }
    public int StackerId { get; set; }

    /// <summary>Records how the association was established for audit and later review.</summary>
    [Required]
    [MaxLength(50)]
    public string MatchMethod { get; set; } = string.Empty;

    /// <summary>Stable SP-0C reason code explaining why persistence was approved.</summary>
    [Required]
    [MaxLength(100)]
    public string ResolutionReasonCode { get; set; } = string.Empty;

    /// <summary>Operator review note when a weak match or duplicate override requires one.</summary>
    [MaxLength(1000)]
    public string? ResolutionNote { get; set; }

    public DateTime LinkedAt { get; set; }
    public int? LinkedByUserId { get; set; }

    [ForeignKey(nameof(SportStackerIdentityId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public SportStackerIdentity SportStackerIdentity { get; set; } = null!;

    [ForeignKey(nameof(StackerId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Stacker Stacker { get; set; } = null!;
}
