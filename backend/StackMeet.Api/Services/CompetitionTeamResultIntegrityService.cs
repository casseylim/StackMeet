using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Dtos;

namespace StackMeet.Api.Services;

/// <summary>
/// Server-side integrity boundary for SQL Doubles/Relay results whose team definitions
/// remain in the competition state document.
/// </summary>
/// <remarks>
/// The browser only offers competition-ready teams for result entry. This service mirrors
/// those readiness rules so an authorized API client cannot create an orphan team result,
/// and a later state save cannot remove/invalidate a team while SQL results still reference it.
/// </remarks>
public sealed class CompetitionTeamResultIntegrityService(StackMeetDbContext database)
{
    public string? ValidateResultUpserts(string? stateJson, IReadOnlyList<ResultUpsertRequest> upserts)
    {
        var teamUpserts = upserts
            .Select(item => new TeamResultReference(
                CompetitionResultRules.NormalizeParticipantType(item.Type),
                item.Participant?.Trim()))
            .Where(item => item.Type is "Doubles" or "Timed Relay")
            .ToArray();

        if (teamUpserts.Length == 0) return null;

        if (!TryReadReadyTeams(stateJson, out var teams, out var stateError))
        {
            return stateError ?? "Competition team state could not be validated.";
        }

        foreach (var item in teamUpserts)
        {
            if (string.IsNullOrWhiteSpace(item.Participant))
                return "Team result participant is required.";

            if (item.Type == "Doubles" && !teams.Doubles.Contains(item.Participant))
                return "Doubles result participant must reference a complete Doubles team in this competition.";

            if (item.Type == "Timed Relay" && !teams.TimedRelays.Contains(item.Participant))
                return "Timed Relay result participant must reference a ready relay team with at least four registered members in this competition.";
        }

        return null;
    }

    public async Task<string?> ValidateStateAgainstExistingResultsAsync(
        int competitionId,
        string? proposedStateJson,
        CancellationToken cancellationToken = default)
    {
        if (!TryReadReadyTeams(proposedStateJson, out var teams, out var stateError))
        {
            return stateError ?? "Competition team state could not be validated.";
        }

        var references = await database.CompetitionResults
            .AsNoTracking()
            .Where(item => item.CompetitionId == competitionId
                && (item.ParticipantType == "Doubles"
                    || item.ParticipantType == "Timed Relay"
                    || item.ParticipantType == "Relay"))
            .Select(item => new { item.ParticipantType, item.ParticipantCode })
            .ToListAsync(cancellationToken);

        foreach (var item in references)
        {
            var type = CompetitionResultRules.NormalizeParticipantType(item.ParticipantType);
            var participant = item.ParticipantCode?.Trim();
            if (string.IsNullOrWhiteSpace(participant)) continue;

            if (type == "Doubles" && !teams.Doubles.Contains(participant))
                return "Competition state cannot remove or invalidate a Doubles team while SQL results reference it.";

            if (type == "Timed Relay" && !teams.TimedRelays.Contains(participant))
                return "Competition state cannot remove or invalidate a Timed Relay team while SQL results reference it.";
        }

        return null;
    }

    public bool TryReadReadyTeams(
        string? stateJson,
        out CompetitionReadyTeamIds teams,
        out string? error)
    {
        teams = CompetitionReadyTeamIds.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(stateJson))
        {
            error = "Competition team state is missing.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(stateJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "Competition team state must be a JSON object.";
                return false;
            }

            var doubles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var relays = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!ReadDoubles(document.RootElement, doubles, out error)
                || !ReadRelays(document.RootElement, relays, out error))
            {
                teams = CompetitionReadyTeamIds.Empty;
                return false;
            }

            teams = new CompetitionReadyTeamIds(doubles, relays);
            return true;
        }
        catch (JsonException)
        {
            error = "Competition team state contains malformed JSON.";
            return false;
        }
    }

    static bool ReadDoubles(JsonElement root, HashSet<string> ready, out string? error)
    {
        error = null;
        if (!root.TryGetProperty("doubles", out var collection)) return true;
        if (collection.ValueKind != JsonValueKind.Array)
        {
            error = "Competition Doubles team state must be an array.";
            return false;
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var team in collection.EnumerateArray())
        {
            if (team.ValueKind != JsonValueKind.Object)
            {
                error = "Competition Doubles team state contains an invalid team entry.";
                return false;
            }

            var id = ReadIdentifier(team, "id");
            if (!string.IsNullOrWhiteSpace(id) && !ids.Add(id))
            {
                error = "Competition Doubles team IDs must be unique.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(id) || !DoublesCanCompete(team)) continue;
            ready.Add(id);
        }

        return true;
    }

    static bool ReadRelays(JsonElement root, HashSet<string> ready, out string? error)
    {
        error = null;
        if (!root.TryGetProperty("relays", out var collection)) return true;
        if (collection.ValueKind != JsonValueKind.Array)
        {
            error = "Competition Relay team state must be an array.";
            return false;
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var team in collection.EnumerateArray())
        {
            if (team.ValueKind != JsonValueKind.Object)
            {
                error = "Competition Relay team state contains an invalid team entry.";
                return false;
            }

            var id = ReadIdentifier(team, "id");
            if (!string.IsNullOrWhiteSpace(id) && !ids.Add(id))
            {
                error = "Competition Relay team IDs must be unique.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(id) || RelayMemberCount(team) < 4) continue;
            ready.Add(id);
        }

        return true;
    }

    static bool DoublesCanCompete(JsonElement team)
    {
        var explicitStatus = ReadString(team, "status");
        if (!string.IsNullOrWhiteSpace(explicitStatus))
            return !explicitStatus.Equals("pending", StringComparison.OrdinalIgnoreCase);

        var explicitType = ReadString(team, "type");
        var division = ReadString(team, "division");
        var type = !string.IsNullOrWhiteSpace(explicitType)
            ? explicitType
            : division?.Contains("parent", StringComparison.OrdinalIgnoreCase) == true
                ? "child_parent"
                : "normal";

        var secondMember =
            ReadIdentifier(team, "two")
            ?? ReadIdentifier(team, "stackerTwoId")
            ?? ReadIdentifier(team, "parentStackerId");
        var parentName = ReadString(team, "parentName") ?? ReadString(team, "partnerName");

        // Mirrors normalizeDoubles()/completedDoubles() in app.js:
        // missing status becomes complete when partner/parent evidence exists or the team is child/parent.
        return !string.IsNullOrWhiteSpace(secondMember)
            || !string.IsNullOrWhiteSpace(parentName)
            || type.Equals("child_parent", StringComparison.OrdinalIgnoreCase);
    }

    static int RelayMemberCount(JsonElement team)
    {
        if (team.TryGetProperty("members", out var members) && members.ValueKind == JsonValueKind.Array)
        {
            return members.EnumerateArray().Count(IsNonBlankIdentifier);
        }

        var count = 0;
        foreach (var propertyName in new[] { "one", "two", "three", "four", "five", "six" })
        {
            if (!string.IsNullOrWhiteSpace(ReadIdentifier(team, propertyName))) count++;
        }

        return count;
    }

    static bool IsNonBlankIdentifier(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => !string.IsNullOrWhiteSpace(element.GetString()),
            JsonValueKind.Number => !string.IsNullOrWhiteSpace(element.GetRawText()),
            _ => false
        };

    static string? ReadIdentifier(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)) return null;
        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString()?.Trim(),
            JsonValueKind.Number => property.GetRawText().Trim(),
            _ => null
        };
    }

    static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString()?.Trim()
            : null;

    sealed record TeamResultReference(string? Type, string? Participant);
}

public sealed record CompetitionReadyTeamIds(
    IReadOnlySet<string> Doubles,
    IReadOnlySet<string> TimedRelays)
{
    public static CompetitionReadyTeamIds Empty { get; } =
        new(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
}
