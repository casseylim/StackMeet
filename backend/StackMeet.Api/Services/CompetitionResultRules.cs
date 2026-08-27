namespace StackMeet.Api.Services;

public static class CompetitionResultRules
{
    public const int MaximumBatchSize = 500;
    public static readonly IReadOnlySet<string> Stages = new HashSet<string>(StringComparer.Ordinal) { "Prelims", "Finals" };
    public static readonly IReadOnlySet<string> ParticipantTypes = new HashSet<string>(StringComparer.Ordinal) { "Individual", "Doubles", "Timed Relay" };
    public static readonly IReadOnlySet<string> Events = new HashSet<string>(StringComparer.Ordinal) { "3-3-3", "3-6-3", "Cycle" };

    public static string? NormalizeStage(string? value) => value?.Trim().ToUpperInvariant() switch { "PRELIMS" => "Prelims", "FINALS" => "Finals", _ => null };
    public static string? NormalizeParticipantType(string? value) => value?.Trim().ToUpperInvariant() switch { "INDIVIDUAL" => "Individual", "DOUBLES" => "Doubles", "TIMED RELAY" or "RELAY" => "Timed Relay", _ => null };
    public static string? NormalizeEvent(string? value) => value?.Trim().ToUpperInvariant() switch { "3-3-3" => "3-3-3", "3-6-3" => "3-6-3", "CYCLE" => "Cycle", _ => null };

    public static IReadOnlyList<string> ValidateIdentity(string? stage, string? type, string? participant, string? eventCode)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(stage) || stage.Trim().Length > 30 || NormalizeStage(stage) is null) errors.Add("Stage must be Prelims or Finals and 30 characters or fewer.");
        if (string.IsNullOrWhiteSpace(type) || type.Trim().Length > 30 || NormalizeParticipantType(type) is null) errors.Add("Participant type is not supported.");
        if (string.IsNullOrWhiteSpace(participant) || participant.Trim().Length > 50) errors.Add("Participant code is required and must be 50 characters or fewer.");
        if (string.IsNullOrWhiteSpace(eventCode) || eventCode.Trim().Length > 50 || NormalizeEvent(eventCode) is null) errors.Add("Event must be 3-3-3, 3-6-3 or Cycle.");
        var canonicalType = NormalizeParticipantType(type); var canonicalEvent = NormalizeEvent(eventCode);
        if (canonicalType == "Timed Relay" && canonicalEvent != "3-6-3") errors.Add("Timed Relay results support only the 3-6-3 event.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateAttempts(IReadOnlyList<decimal>? attempts, decimal penalty)
    {
        var errors = new List<string>();
        if (attempts is null || attempts.Count is < 1 or > 3) errors.Add("A result must contain between 1 and 3 attempts.");
        else if (attempts.Any(value => value < 0 || value > 86400 || decimal.Round(value, 3) != value)) errors.Add("Attempts must be finite values from 0 through 86400 with at most 3 decimals.");
        if (penalty < 0 || penalty > 999 || decimal.Round(penalty, 3) != penalty) errors.Add("Penalty must be between 0 and 999 with at most 3 decimals.");
        return errors;
    }

    public static string? NormalizeAndValidate(string? value, Func<string?, string?> normalizer, int maxLength) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > maxLength ? null : normalizer(value);
}
