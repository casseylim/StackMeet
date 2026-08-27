using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Dtos;
using StackMeet.Api.Models;

namespace StackMeet.Api.Services;

/// <summary>
/// Central validation and normalization boundary for SQL-native competition result writes.
/// </summary>
public static class CompetitionResultValidator
{
    public const int MaxBatchChanges = 1000;
    public const int MaxAttemptsPerResult = 3;
    const int MaxParticipantLength = 50;
    const int ParticipantLookupChunkSize = 300;
    const decimal MaxAttemptSeconds = 86400m;
    const decimal MaxPenalty = 999999999.999m;

    public static bool TryNormalize(ResultBatchRequest? request, out ResultBatchRequest normalized, out string error)
    {
        normalized = new ResultBatchRequest([], []);
        error = string.Empty;

        if (request?.Upserts is null || request.Deletes is null)
        {
            error = "Result upserts and deletes are required.";
            return false;
        }

        var changeCount = request.Upserts.Length + request.Deletes.Length;
        if (changeCount == 0)
        {
            error = "At least one result change is required.";
            return false;
        }

        if (changeCount > MaxBatchChanges)
        {
            error = $"A result batch cannot exceed {MaxBatchChanges} changes.";
            return false;
        }

        var upserts = new ResultUpsertRequest[request.Upserts.Length];
        for (var index = 0; index < request.Upserts.Length; index++)
        {
            var item = request.Upserts[index];
            if (!TryIdentity(item.Stage, item.Type, item.Participant, item.Event, out var stage, out var type, out var participant, out var eventCode, out error)) return false;
            if (item.Attempts is null || item.Attempts.Length is < 1 or > MaxAttemptsPerResult)
            {
                error = $"Result attempts must contain between 1 and {MaxAttemptsPerResult} values.";
                return false;
            }
            if (item.Attempts.Any(value => value <= 0 || value > MaxAttemptSeconds || !HasAtMostThreeDecimalPlaces(value)))
            {
                error = "Each result attempt must be greater than 0, no more than 86400 seconds, and have at most 3 decimal places.";
                return false;
            }
            if (item.Penalty < 0 || item.Penalty > MaxPenalty || !HasAtMostThreeDecimalPlaces(item.Penalty))
            {
                error = "Result penalty must be between 0 and 999999999.999 and have at most 3 decimal places.";
                return false;
            }
            if (item.ExpectedRevision is < 0)
            {
                error = "ExpectedRevision cannot be negative.";
                return false;
            }

            upserts[index] = item with
            {
                Stage = stage,
                Type = type,
                Participant = participant,
                Event = eventCode
            };
        }

        var deletes = new ResultDeleteRequest[request.Deletes.Length];
        for (var index = 0; index < request.Deletes.Length; index++)
        {
            var item = request.Deletes[index];
            if (!TryIdentity(item.Stage, item.Type, item.Participant, item.Event, out var stage, out var type, out var participant, out var eventCode, out error)) return false;
            if (item.ExpectedRevision is < 0)
            {
                error = "ExpectedRevision cannot be negative.";
                return false;
            }

            deletes[index] = item with
            {
                Stage = stage,
                Type = type,
                Participant = participant,
                Event = eventCode
            };
        }

        var logicalKeys = upserts.Select(Key).Concat(deletes.Select(Key)).ToArray();
        if (logicalKeys.Length != logicalKeys.Distinct(StringComparer.Ordinal).Count())
        {
            error = "Duplicate logical result in batch.";
            return false;
        }

        normalized = new ResultBatchRequest(upserts, deletes);
        return true;
    }

    /// <summary>
    /// Validates participant identities for upserts only. Deletes remain available for cleaning up
    /// orphaned legacy rows that pre-date the SQL-native integrity rules.
    /// </summary>
    public static async Task<string?> ValidateUpsertParticipants(
        StackMeetDbContext database,
        Competition competition,
        IReadOnlyCollection<ResultUpsertRequest> upserts,
        CancellationToken ct)
    {
        if (upserts.Count == 0) return null;

        var individualCodes = upserts
            .Where(item => item.Type == "Individual")
            .Select(item => item.Participant)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (individualCodes.Length > 0)
        {
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var chunk in individualCodes.Chunk(ParticipantLookupChunkSize))
            {
                var found = await database.Stackers
                    .AsNoTracking()
                    .Where(item => item.CompetitionId == competition.Id && chunk.Contains(item.StackerCode))
                    .Select(item => item.StackerCode)
                    .ToListAsync(ct);
                existing.UnionWith(found);
            }

            var missing = individualCodes.FirstOrDefault(code => !existing.Contains(code));
            if (missing is not null) return $"Participant '{missing}' does not exist as an Individual participant in this competition.";
        }

        var doublesCodes = upserts
            .Where(item => item.Type == "Doubles")
            .Select(item => item.Participant)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var relayCodes = upserts
            .Where(item => item.Type == "Timed Relay")
            .Select(item => item.Participant)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (doublesCodes.Length == 0 && relayCodes.Length == 0) return null;

        var json = await database.CompetitionStates
            .AsNoTracking()
            .Where(item => item.CompetitionKey == competition.CompetitionKey)
            .Select(item => item.JsonData)
            .SingleOrDefaultAsync(ct);
        var doubles = TeamIds(json, "doubles");
        var relays = TeamIds(json, "relays");

        var missingDouble = doublesCodes.FirstOrDefault(code => !doubles.Contains(code));
        if (missingDouble is not null) return $"Participant '{missingDouble}' does not exist as a Doubles participant in this competition.";
        var missingRelay = relayCodes.FirstOrDefault(code => !relays.Contains(code));
        if (missingRelay is not null) return $"Participant '{missingRelay}' does not exist as a Timed Relay participant in this competition.";
        return null;
    }

    static bool TryIdentity(
        string? rawStage,
        string? rawType,
        string? rawParticipant,
        string? rawEvent,
        out string stage,
        out string type,
        out string participant,
        out string eventCode,
        out string error)
    {
        stage = CanonicalStage(rawStage) ?? string.Empty;
        type = CanonicalType(rawType) ?? string.Empty;
        participant = rawParticipant?.Trim() ?? string.Empty;
        eventCode = CanonicalEvent(rawEvent) ?? string.Empty;
        error = string.Empty;

        if (stage.Length == 0)
        {
            error = "Result stage must be Prelims or Finals.";
            return false;
        }
        if (type.Length == 0)
        {
            error = "Result type must be Individual, Doubles or Timed Relay.";
            return false;
        }
        if (participant.Length == 0 || participant.Length > MaxParticipantLength)
        {
            error = $"Result participant code must be 1-{MaxParticipantLength} characters.";
            return false;
        }
        if (eventCode.Length == 0)
        {
            error = "Result event must be 3-3-3, 3-6-3 or Cycle.";
            return false;
        }
        return true;
    }

    static string? CanonicalStage(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "PRELIMS" => "Prelims",
        "FINALS" => "Finals",
        _ => null
    };

    static string? CanonicalType(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "INDIVIDUAL" => "Individual",
        "DOUBLES" => "Doubles",
        "TIMED RELAY" => "Timed Relay",
        // Compatibility alias retained for older clients; new writes are persisted canonically.
        "RELAY" => "Timed Relay",
        _ => null
    };

    static string? CanonicalEvent(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "3-3-3" => "3-3-3",
        "3-6-3" => "3-6-3",
        "CYCLE" => "Cycle",
        _ => null
    };

    static bool HasAtMostThreeDecimalPlaces(decimal value) => decimal.Round(value, 3) == value;

    static HashSet<string> TeamIds(string? json, string arrayName)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json)) return ids;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return ids;
            JsonElement teams = default;
            var found = false;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, arrayName, StringComparison.OrdinalIgnoreCase)) continue;
                teams = property.Value;
                found = true;
                break;
            }
            if (!found || teams.ValueKind != JsonValueKind.Array) return ids;
            foreach (var team in teams.EnumerateArray())
            {
                if (team.ValueKind != JsonValueKind.Object) continue;
                foreach (var property in team.EnumerateObject())
                {
                    if (!string.Equals(property.Name, "id", StringComparison.OrdinalIgnoreCase) || property.Value.ValueKind != JsonValueKind.String) continue;
                    var id = property.Value.GetString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(id)) ids.Add(id);
                    break;
                }
            }
        }
        catch (JsonException)
        {
            // Malformed legacy state cannot establish a valid team identity.
        }
        return ids;
    }

    static string Key(ResultUpsertRequest item) => Key(item.Stage, item.Type, item.Participant, item.Event);
    static string Key(ResultDeleteRequest item) => Key(item.Stage, item.Type, item.Participant, item.Event);
    static string Key(string stage, string type, string participant, string eventCode) =>
        string.Join("\u001f", stage.ToUpperInvariant(), type.ToUpperInvariant(), participant.ToUpperInvariant(), eventCode.ToUpperInvariant());
}
