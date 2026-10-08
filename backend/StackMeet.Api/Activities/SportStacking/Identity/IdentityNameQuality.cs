namespace StackMeet.Api.Activities.SportStacking.Identity;

// Reject punctuation-only import placeholders; preserve legitimate hyphenated names.
public static class IdentityNameQuality
{
    public static bool IsUsable(string? value) => !string.IsNullOrWhiteSpace(value) && value.Any(char.IsLetterOrDigit);
}
