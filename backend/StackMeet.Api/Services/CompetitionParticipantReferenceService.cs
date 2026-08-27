using System.Text.Json;

namespace StackMeet.Api.Services;

public sealed class CompetitionParticipantReferenceService
{
    static readonly HashSet<string> ReferenceFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "one", "two", "three", "four", "five", "six",
        "member", "members", "participant", "participantCode", "stackerCode",
        "selectedQualifiers", "participantIds", "participantCodes",
        "stackerOneId", "stackerTwoId", "childStackerId", "parentStackerId"
    };

    public bool ContainsParticipant(string? json, string participantCode)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(participantCode)) return false;
        var code = participantCode.Trim();
        try
        {
            using var document = JsonDocument.Parse(json);
            return Visit(document.RootElement, code);
        }
        catch (JsonException)
        {
            // Deletion safety must fail closed when legacy state cannot be inspected reliably.
            return true;
        }
    }

    public IReadOnlySet<string> ExtractReferencedCodes(string? json)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            using var document = JsonDocument.Parse(json);
            Collect(document.RootElement, result);
            return result;
        }
        catch (JsonException)
        {
            throw;
        }
    }

    static bool Visit(JsonElement element, string code)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (ReferenceFields.Contains(property.Name) && ContainsReferenceValue(property.Value, code)) return true;
                if (Visit(property.Value, code)) return true;
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                if (Visit(child, code)) return true;
        }

        return false;
    }

    static void Collect(JsonElement element, HashSet<string> result)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (ReferenceFields.Contains(property.Name)) AddValues(property.Value, result);
                Collect(property.Value, result);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray()) Collect(child, result);
        }
    }

    static void AddValues(JsonElement element, HashSet<string> result)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(value)) result.Add(value);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray()) AddValues(child, result);
        }
    }

    static bool ContainsReferenceValue(JsonElement element, string code)
    {
        if (element.ValueKind == JsonValueKind.String)
            return string.Equals(element.GetString()?.Trim(), code, StringComparison.OrdinalIgnoreCase);

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                if (ContainsReferenceValue(child, code)) return true;
        }

        return false;
    }
}
