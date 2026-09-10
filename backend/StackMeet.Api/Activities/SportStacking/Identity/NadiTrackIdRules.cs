namespace StackMeet.Api.Activities.SportStacking.Identity;

/// <summary>
/// Defines the public NADITrack identifier contract for permanent Sport Stacking identities.
/// The public identifier is intentionally separate from database primary keys and competition-scoped StackerCode values.
/// </summary>
public static class NadiTrackIdRules
{
    public const string Prefix = "NDT-";
    public const int BodyLength = 7;

    // Excludes visually ambiguous 0/O and 1/I/L characters.
    public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    public static int TotalLength => Prefix.Length + BodyLength;

    public static string Normalize(string value) => value.Trim().ToUpperInvariant();

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var normalized = Normalize(value);
        if (normalized.Length != TotalLength || !normalized.StartsWith(Prefix, StringComparison.Ordinal)) return false;

        return normalized.AsSpan(Prefix.Length).ToString().All(Alphabet.Contains);
    }
}
