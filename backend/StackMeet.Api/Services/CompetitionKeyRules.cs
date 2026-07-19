using System.Text.RegularExpressions;

namespace StackMeet.Api.Services;

public static partial class CompetitionKeyRules
{
    public static bool IsValid(string? value) => !string.IsNullOrWhiteSpace(value) && KeyRegex().IsMatch(value);
    public static string Normalize(string value) => value.Trim().ToUpperInvariant();

    [GeneratedRegex("^[A-Z0-9][A-Z0-9_-]{2,49}$")]
    private static partial Regex KeyRegex();
}