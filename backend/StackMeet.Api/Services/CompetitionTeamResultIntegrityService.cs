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
/// and a later state save cannot remove, invalidate or silently reassign a team while SQL
/// results still reference it.
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

        if (!TryReadState(stateJson, out var state, out var stateError))
        {
            return stateError ?? "Competition team state could not be validated.";
        }

        foreach (var item in teamUpserts)
        {
            if (string.IsNullOrWhiteSpace(item.Participant))
                return "Team result participant is required.";

            if (item.Type == "Doubles" && !state.ReadyDoubles.Contains(item.Participant))
                return "Doubles result participant must reference a complete Doubles team in this competition.";

            if (item.Type == "Timed Relay" && !state.ReadyRelays.Contains(item.Participant))
                return "Timed Relay result participant must reference a ready relay team with at least four registered members in this competition.";
        }

        return null;
    }

    public async Task<string?> ValidateResultUpsertsAsync(
        int competitionId,
        string? stateJson,
        IReadOnlyList<ResultUpsertRequest> upserts,
        CancellationToken cancellationToken = default)
    {
        var teamUpserts = upserts
            .Select(item => new TeamResultReference(
                CompetitionResultRules.NormalizeParticipantType(item.Type),
                item.Participant?.Trim()))
            .Where(item => item.Type is "Doubles" or "Timed Relay")
            .ToArray();

        if (teamUpserts.Length == 0) return null;

        if (!TryReadState(stateJson, out var state, out var stateError))
        {
            return stateError ?? "Competition team state could not be validated.";
        }

        var memberCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in teamUpserts)
        {
            if (string.IsNullOrWhiteSpace(item.Participant))
                return "Team result participant is required.";

            if (item.Type == "Doubles")
            {
                if (!state.ReadyDoubles.Contains(item.Participant))
                    return "Doubles result participant must reference a complete Doubles team in this competition.";

                if (!state.DoublesMembers.TryGetValue(item.Participant, out var members) || members.Count == 0)
                    return "Doubles result participant must resolve to registered Stackers in this competition.";

                foreach (var member in members) memberCodes.Add(member);
            }

            if (item.Type == "Timed Relay")
            {
                if (!state.ReadyRelays.Contains(item.Participant))
                    return "Timed Relay result participant must reference a ready relay team with at least four registered members in this competition.";

                if (!state.RelaysMembers.TryGetValue(item.Participant, out var members) || members.Count < 4)
                    return "Timed Relay result participant must resolve to at least four registered Stackers in this competition.";

                foreach (var member in members) memberCodes.Add(member);
            }
        }

        foreach (var chunk in memberCodes.Chunk(500))
        {
            var existing = await database.Stackers
                .AsNoTracking()
                .Where(item => item.CompetitionId == competitionId && chunk.Contains(item.StackerCode))
                .Select(item => item.StackerCode)
                .ToListAsync(cancellationToken);

            if (existing.Count != chunk.Length)
                return "Team result members must belong to this competition's registered Stackers.";
        }

        return null;
    }

    public async Task<string?> ValidateStateAgainstExistingResultsAsync(
        int competitionId,
        string? currentStateJson,
        string? proposedStateJson,
        CancellationToken cancellationToken = default)
    {
        if (!TryReadState(proposedStateJson, out var proposed, out var proposedError))
        {
            return proposedError ?? "Competition team state could not be validated.";
        }

        var references = await database.CompetitionResults
            .AsNoTracking()
            .Where(item => item.CompetitionId == competitionId
                && (item.ParticipantType == "Doubles"
                    || item.ParticipantType == "Timed Relay"
                    || item.ParticipantType == "Relay"))
            .Select(item => new { item.ParticipantType, item.ParticipantCode })
            .ToListAsync(cancellationToken);

        if (references.Count == 0) return null;

        if (!TryReadState(currentStateJson, out var current, out _))
        {
            return "Current competition team state cannot be validated while SQL team results exist.";
        }

        foreach (var item in references)
        {
            var type = CompetitionResultRules.NormalizeParticipantType(item.ParticipantType);
            var participant = item.ParticipantCode?.Trim();
            if (string.IsNullOrWhiteSpace(participant)) continue;

            if (type == "Doubles")
            {
                if (!proposed.ReadyDoubles.Contains(participant))
                    return "Competition state cannot remove or invalidate a Doubles team while SQL results reference it.";

                if (!current.DoublesComposition.TryGetValue(participant, out var before)
                    || !proposed.DoublesComposition.TryGetValue(participant, out var after))
                {
                    return "Existing Doubles result references a team that cannot be resolved safely in competition state.";
                }

                if (!string.Equals(before, after, StringComparison.Ordinal))
                    return "Competition state cannot change Doubles team members while SQL results reference it.";
            }

            if (type == "Timed Relay")
            {
                if (!proposed.ReadyRelays.Contains(participant))
                    return "Competition state cannot remove or invalidate a Timed Relay team while SQL results reference it.";

                if (!current.RelaysComposition.TryGetValue(participant, out var before)
                    || !proposed.RelaysComposition.TryGetValue(participant, out var after))
                {
                    return "Existing Timed Relay result references a team that cannot be resolved safely in competition state.";
                }

                if (!string.Equals(before, after, StringComparison.Ordinal))
                    return "Competition state cannot change Timed Relay team members while SQL results reference it.";
            }
        }

        return null;
    }

    public bool TryReadReadyTeams(
        string? stateJson,
        out CompetitionReadyTeamIds teams,
        out string? error)
    {
        if (!TryReadState(stateJson, out var state, out error))
        {
            teams = CompetitionReadyTeamIds.Empty;
            return false;
        }

        teams = new CompetitionReadyTeamIds(state.ReadyDoubles, state.ReadyRelays);
        return true;
    }

    public bool TryReadReadyTeamMemberships(
        string? stateJson,
        out CompetitionReadyTeamMemberships teams,
        out string? error)
    {
        if (!TryReadState(stateJson, out var state, out error))
        {
            teams = CompetitionReadyTeamMemberships.Empty;
            return false;
        }

        var doubles = state.DoublesMembers
            .Where(item => state.ReadyDoubles.Contains(item.Key))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        var relays = state.RelaysMembers
            .Where(item => state.ReadyRelays.Contains(item.Key))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

        teams = new CompetitionReadyTeamMemberships(doubles, relays);
        return true;
    }

    bool TryReadState(
        string? stateJson,
        out CompetitionTeamStateSnapshot state,
        out string? error)
    {
        state = CompetitionTeamStateSnapshot.Empty;
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

            var readyDoubles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var readyRelays = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var doublesComposition = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var relaysComposition = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var doublesMembers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            var relaysMembers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

            if (!ReadDoubles(document.RootElement, readyDoubles, doublesComposition, doublesMembers, out error)
                || !ReadRelays(document.RootElement, readyRelays, relaysComposition, relaysMembers, out error))
            {
                state = CompetitionTeamStateSnapshot.Empty;
                return false;
            }

            state = new CompetitionTeamStateSnapshot(
                readyDoubles,
                readyRelays,
                doublesComposition,
                relaysComposition,
                doublesMembers,
                relaysMembers);
            return true;
        }
        catch (JsonException)
        {
            error = "Competition team state contains malformed JSON.";
            return false;
        }
    }

    static bool ReadDoubles(
        JsonElement root,
        HashSet<string> ready,
        Dictionary<string, string> composition,
        Dictionary<string, IReadOnlyList<string>> membersByTeam,
        out string? error)
    {
        error = null;
        if (!root.TryGetProperty("doubles", out var collection)) return true;
        if (collection.ValueKind != JsonValueKind.Array)
        {
            error = "Competition Doubles team state must be an array.";
            return false;
        }

        foreach (var team in collection.EnumerateArray())
        {
            if (team.ValueKind != JsonValueKind.Object)
            {
                error = "Competition Doubles team state contains an invalid team entry.";
                return false;
            }

            var id = ReadIdentifier(team, "id");
            if (string.IsNullOrWhiteSpace(id)) continue;

            var members = DoublesMemberIds(team);
            if (members.Count != members.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            {
                error = "A Doubles team cannot contain the same registered Stacker twice.";
                return false;
            }

            if (!composition.TryAdd(id, DoublesComposition(team))
                || !membersByTeam.TryAdd(id, members))
            {
                error = "Competition Doubles team IDs must be unique.";
                return false;
            }

            if (DoublesCanCompete(team, members)) ready.Add(id);
        }

        return true;
    }

    static bool ReadRelays(
        JsonElement root,
        HashSet<string> ready,
        Dictionary<string, string> composition,
        Dictionary<string, IReadOnlyList<string>> membersByTeam,
        out string? error)
    {
        error = null;
        if (!root.TryGetProperty("relays", out var collection)) return true;
        if (collection.ValueKind != JsonValueKind.Array)
        {
            error = "Competition Relay team state must be an array.";
            return false;
        }

        foreach (var team in collection.EnumerateArray())
        {
            if (team.ValueKind != JsonValueKind.Object)
            {
                error = "Competition Relay team state contains an invalid team entry.";
                return false;
            }

            var id = ReadIdentifier(team, "id");
            if (string.IsNullOrWhiteSpace(id)) continue;

            var members = RelayMemberIds(team);
            if (members.Count != members.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            {
                error = "A Relay team cannot contain the same registered Stacker twice.";
                return false;
            }

            if (!composition.TryAdd(id, string.Join("\u001f", members.Select(NormalizeComponent)))
                || !membersByTeam.TryAdd(id, members))
            {
                error = "Competition Relay team IDs must be unique.";
                return false;
            }

            if (members.Count >= 4) ready.Add(id);
        }

        return true;
    }

    static bool DoublesCanCompete(JsonElement team, IReadOnlyList<string> members)
    {
        var explicitStatus = ReadString(team, "status");
        if (explicitStatus?.Equals("pending", StringComparison.OrdinalIgnoreCase) == true)
            return false;

        var explicitType = ReadString(team, "type");
        var division = ReadString(team, "division");
        var parentName = ReadString(team, "parentName") ?? ReadString(team, "partnerName");
        var type = !string.IsNullOrWhiteSpace(explicitType)
            ? explicitType
            : !string.IsNullOrWhiteSpace(parentName)
                || division?.Contains("parent", StringComparison.OrdinalIgnoreCase) == true
                    ? "child_parent"
                    : "normal";
        if (members.Count == 0) return false;

        if (type.Equals("child_parent", StringComparison.OrdinalIgnoreCase))
            return members.Count >= 2 || !string.IsNullOrWhiteSpace(parentName);

        return members.Count >= 2;
    }

    static IReadOnlyList<string> DoublesMemberIds(JsonElement team)
    {
        var one =
            ReadIdentifier(team, "one")
            ?? ReadIdentifier(team, "stackerOneId")
            ?? ReadIdentifier(team, "childStackerId");
        var two = DoublesSecondMember(team);

        return new[] { one, two }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
    }

    static string DoublesComposition(JsonElement team)
    {
        var members = DoublesMemberIds(team);
        var parentName = ReadString(team, "parentName") ?? ReadString(team, "partnerName");

        return string.Join(
            "\u001f",
            members.Select(NormalizeComponent).Append(NormalizeComponent(parentName)));
    }

    static string? DoublesSecondMember(JsonElement team) =>
        ReadIdentifier(team, "two")
        ?? ReadIdentifier(team, "stackerTwoId")
        ?? ReadIdentifier(team, "parentStackerId");

    static IReadOnlyList<string> RelayMemberIds(JsonElement team)
    {
        if (team.TryGetProperty("members", out var members) && members.ValueKind == JsonValueKind.Array)
        {
            return members
                .EnumerateArray()
                .Select(ReadIdentifier)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToArray();
        }

        var result = new List<string>(6);
        foreach (var propertyName in new[] { "one", "two", "three", "four", "five", "six" })
        {
            var value = ReadIdentifier(team, propertyName);
            if (!string.IsNullOrWhiteSpace(value)) result.Add(value);
        }

        return result;
    }

    static string? ReadIdentifier(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString()?.Trim(),
            JsonValueKind.Number => element.GetRawText().Trim(),
            _ => null
        };

    static string? ReadIdentifier(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)) return null;
        return ReadIdentifier(property);
    }

    static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString()?.Trim()
            : null;

    static string NormalizeComponent(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();

    sealed record TeamResultReference(string? Type, string? Participant);

    sealed record CompetitionTeamStateSnapshot(
        IReadOnlySet<string> ReadyDoubles,
        IReadOnlySet<string> ReadyRelays,
        IReadOnlyDictionary<string, string> DoublesComposition,
        IReadOnlyDictionary<string, string> RelaysComposition,
        IReadOnlyDictionary<string, IReadOnlyList<string>> DoublesMembers,
        IReadOnlyDictionary<string, IReadOnlyList<string>> RelaysMembers)
    {
        public static CompetitionTeamStateSnapshot Empty { get; } =
            new(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase));
    }
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

public sealed record CompetitionReadyTeamMemberships(
    IReadOnlyDictionary<string, IReadOnlyList<string>> Doubles,
    IReadOnlyDictionary<string, IReadOnlyList<string>> TimedRelays)
{
    public static CompetitionReadyTeamMemberships Empty { get; } =
        new(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase));
}
