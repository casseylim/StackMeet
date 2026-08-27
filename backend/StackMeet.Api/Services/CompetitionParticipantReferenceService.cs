using System.Text.Json;

namespace StackMeet.Api.Services;

public sealed class CompetitionParticipantReferenceService
{
    static readonly HashSet<string> ReferenceFields = new(StringComparer.OrdinalIgnoreCase)
    { "one", "two", "three", "four", "five", "six", "member", "members", "participant", "participantCode", "stackerCode", "selectedQualifiers", "participantIds", "participantCodes" };
    public bool ContainsParticipant(string? json, string participantCode)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try { using var document = JsonDocument.Parse(json); return Visit(document.RootElement, participantCode); }
        catch (JsonException) { return false; }
    }
    static bool Visit(JsonElement element, string code)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject())
                if (ReferenceFields.Contains(property.Name) && property.Value.ValueKind == JsonValueKind.String && string.Equals(property.Value.GetString()?.Trim(), code, StringComparison.Ordinal)) return true;
                else if (Visit(property.Value, code)) return true;
        if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) if (Visit(child, code)) return true;
        return false;
    }
}
