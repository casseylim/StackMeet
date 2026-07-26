namespace StackMeet.Api.Services;

/// <summary>
/// Normalizes and validates email login names for Phase 1 accounts.
/// </summary>
/// <remarks>
/// This is intentionally lightweight until full ASP.NET Identity or email confirmation is introduced.
/// </remarks>
public static class EmailRules
{
    /// <summary>
    /// Converts an email address to the unique lookup form stored in the database.
    /// </summary>
    /// <remarks>
    /// Normalized email is the value used by unique indexes and login lookups.
    /// </remarks>
    public static string Normalize(string? email) => (email ?? "").Trim().ToUpperInvariant();

    /// <summary>
    /// Performs minimal email shape validation for manually created accounts.
    /// </summary>
    /// <remarks>
    /// Phase 2 email confirmation should replace this with stronger ownership verification.
    /// </remarks>
    public static bool IsValid(string? email)
    {
        var value = (email ?? "").Trim();
        var at = value.IndexOf('@');
        return value.Length <= 200
            && at > 0
            && at < value.Length - 1
            && value.LastIndexOf('@') == at;
    }
}
