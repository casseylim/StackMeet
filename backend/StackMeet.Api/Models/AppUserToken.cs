namespace StackMeet.Api.Models;

/// <summary>
/// Stores one-time account activation and password-reset tokens.
/// </summary>
/// <remarks>
/// Only the token hash is stored; the raw token exists only in the email link sent to the user.
/// </remarks>
public sealed class AppUserToken
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string Purpose { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
