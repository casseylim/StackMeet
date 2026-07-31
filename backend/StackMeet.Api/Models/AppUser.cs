namespace StackMeet.Api.Models;

/// <summary>
/// Represents a person who can sign in to StackMeet with an email address.
/// </summary>
/// <remarks>
/// Roles are not stored directly on the user except for the global system-admin flag;
/// competition-specific authority belongs in CompetitionUser.
/// </remarks>
public sealed class AppUser
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool EmailConfirmed { get; set; }
    public bool IsSystemAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    /// <summary>Counts the current failed-password streak before a staged lockout.</summary>
    public int FailedLoginAttempts { get; set; }
    /// <summary>Counts completed temporary lockout rounds; the third round becomes permanent.</summary>
    public int LoginLockoutRound { get; set; }
    /// <summary>Ends the current temporary lockout, or remains null for a permanent lockout.</summary>
    public DateTime? LockoutUntil { get; set; }
    /// <summary>Marks an account that requires password reset before it can log in again.</summary>
    public bool IsPermanentlyLocked { get; set; }
    public ICollection<CompetitionUser> CompetitionUsers { get; set; } = [];
    public ICollection<AppUserToken> Tokens { get; set; } = [];
}
