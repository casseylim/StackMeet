using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Services;

namespace StackMeet.Api.Activities.SportStacking.Identity;

public static class IdentityLinkedTeamCareerBlockers
{
    public const string IdentityLinkAmbiguous = "identity-link-ambiguous";
}

/// <summary>
/// Provenance for one identity-linked team career fact. The membership fingerprint binds the
/// result to the validated competition-time team composition without exposing teammate identifiers.
/// </summary>
public sealed record IdentityLinkedTeamCareerEvidence(
    long CompetitionStateRevision,
    long CompetitionResultsRevision,
    long ResultRevision,
    string MembershipSha256);

/// <summary>
/// One finalized/public Doubles or Timed Relay performance linked to a permanent Stacker identity.
/// Team membership is resolved only through the server-validated competition state; no name matching occurs.
/// </summary>
public sealed record IdentityLinkedTeamCareerPoint(
    string CompetitionKey,
    string CompetitionName,
    DateOnly CompetitionDate,
    string ParticipantType,
    string TeamCode,
    string Stage,
    string EventCode,
    string ResultStatus,
    decimal? OfficialBestTime,
    decimal? RawBestTime,
    decimal AppliedPenalty,
    int RegisteredMemberCount,
    bool HasExternalPartner,
    IdentityLinkedTeamCareerEvidence Evidence);

/// <summary>
/// SP-4Q internal read-only career foundation. This type is intentionally not part of the public profile contract.
/// </summary>
public sealed record IdentityLinkedTeamCareerReadModel(
    string NadiTrackId,
    IReadOnlyList<IdentityLinkedTeamCareerPoint> History);

/// <summary>
/// SP-4Q read-only bridge from reviewed competition Stacker identity links to finalized team results.
/// </summary>
/// <remarks>
/// The permanent identity link supplies only the competition-scoped Stacker code. Doubles/Relay membership is
/// interpreted by CompetitionTeamResultIntegrityService, the same server boundary that prevents team composition
/// drift while SQL team results exist. Only finalized publicly-listed competitions are eligible. The service does
/// not publish a controller contract, infer identities from names, or assign individual medals/ranks from team data.
/// </remarks>
public sealed class IdentityLinkedTeamCareerService(StackMeetDbContext database)
{
    public const string MembershipEvidenceVersion = "sp4q-team-membership-v1";

    public async Task<IdentityLinkedTeamCareerReadModel?> GetPublicEligibleAsync(
        string? nadiTrackId,
        CancellationToken cancellationToken = default)
    {
        if (!NadiTrackIdRules.IsValid(nadiTrackId)) return null;
        var normalizedId = NadiTrackIdRules.Normalize(nadiTrackId!);

        var identity = await database.SportStackerIdentities
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.NadiTrackId == normalizedId && item.IsPublicProfile,
                cancellationToken);

        if (identity is null) return null;

        var linkedRows = await (
            from link in database.StackerIdentityLinks.AsNoTracking()
            join stacker in database.Stackers.AsNoTracking() on link.StackerId equals stacker.Id
            join competition in database.Competitions.AsNoTracking() on stacker.CompetitionId equals competition.Id
            where link.SportStackerIdentityId == identity.Id
                && competition.IsPubliclyListed
                && (competition.Status == "Closed"
                    || competition.Status == "Archived"
                    || competition.ArchivedAt != null)
            select new LinkedCompetitionRow(
                competition.Id,
                competition.CompetitionKey,
                competition.CompetitionName,
                competition.StartDate,
                competition.ResultsRevision,
                stacker.StackerCode))
            .ToListAsync(cancellationToken);

        var linkedByCompetition = new Dictionary<int, LinkedCompetitionRow>();
        foreach (var group in linkedRows.GroupBy(item => item.CompetitionId))
        {
            if (group.Count() != 1)
            {
                throw Blocked(
                    IdentityLinkedTeamCareerBlockers.IdentityLinkAmbiguous,
                    $"Permanent identity has {group.Count()} reviewed Stacker links for competition {group.Key}; team history cannot choose one implicitly.");
            }

            var row = group.Single();
            if (string.IsNullOrWhiteSpace(row.ParticipantCode))
            {
                throw Blocked(
                    IdentityLinkedTeamCareerBlockers.IdentityLinkAmbiguous,
                    $"Reviewed identity link for competition {group.Key} has no participant code.");
            }

            linkedByCompetition.Add(group.Key, row);
        }

        if (linkedByCompetition.Count == 0)
            return new IdentityLinkedTeamCareerReadModel(identity.NadiTrackId, []);

        var competitionKeys = linkedByCompetition.Values
            .Select(item => item.CompetitionKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var stateRows = await database.CompetitionStates
            .AsNoTracking()
            .Where(item => competitionKeys.Contains(item.CompetitionKey))
            .Select(item => new TeamStateRow(item.CompetitionKey, item.JsonData, item.StateRevision))
            .ToListAsync(cancellationToken);
        var stateByCompetitionKey = stateRows
            .GroupBy(item => item.CompetitionKey, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

        var competitionIds = linkedByCompetition.Keys.ToArray();
        var resultRows = await database.CompetitionResults
            .AsNoTracking()
            .Where(item => competitionIds.Contains(item.CompetitionId))
            .Select(item => new TeamResultRow(
                item.CompetitionId,
                item.Id,
                item.Stage,
                item.ParticipantType,
                item.ParticipantCode,
                item.EventCode,
                item.AttemptsJson,
                item.Penalty,
                item.Revision))
            .ToListAsync(cancellationToken);

        var membershipReader = new CompetitionTeamResultIntegrityService(database);
        var history = new List<IdentityLinkedTeamCareerPoint>();

        foreach (var linked in linkedByCompetition.Values)
        {
            if (!stateByCompetitionKey.TryGetValue(linked.CompetitionKey, out var state)) continue;
            if (!membershipReader.TryReadReadyTeamMemberships(state.JsonData, out var memberships, out _)) continue;

            var candidates = resultRows
                .Where(item => item.CompetitionId == linked.CompetitionId)
                .Select(item => TryCreateCandidate(linked, state, memberships, item))
                .Where(item => item is not null)
                .Select(item => item!)
                .ToList();

            // Legacy storage can theoretically contain both Relay and Timed Relay aliases for one logical row.
            // A duplicate normalized logical result is ambiguous and contributes no career fact.
            foreach (var group in candidates.GroupBy(item => item.LogicalKey, StringComparer.Ordinal))
            {
                if (group.Count() != 1) continue;
                var item = group.Single();

                history.Add(new IdentityLinkedTeamCareerPoint(
                    linked.CompetitionKey,
                    linked.CompetitionName,
                    linked.CompetitionDate,
                    item.ParticipantType,
                    item.TeamCode,
                    item.Stage,
                    item.EventCode,
                    item.ResultStatus,
                    item.OfficialBestTime,
                    item.RawBestTime,
                    item.AppliedPenalty,
                    item.RegisteredMemberCount,
                    item.HasExternalPartner,
                    new IdentityLinkedTeamCareerEvidence(
                        state.StateRevision,
                        linked.ResultsRevision,
                        item.ResultRevision,
                        item.MembershipSha256)));
            }
        }

        var ordered = history
            .OrderBy(item => item.CompetitionDate)
            .ThenBy(item => item.CompetitionKey, StringComparer.Ordinal)
            .ThenBy(item => TeamTypeSort(item.ParticipantType))
            .ThenBy(item => item.TeamCode, StringComparer.Ordinal)
            .ThenBy(item => StageSort(item.Stage))
            .ThenBy(item => EventSort(item.EventCode))
            .ToArray();

        return new IdentityLinkedTeamCareerReadModel(identity.NadiTrackId, ordered);
    }

    private static TeamCareerCandidate? TryCreateCandidate(
        LinkedCompetitionRow linked,
        TeamStateRow state,
        CompetitionReadyTeamMemberships memberships,
        TeamResultRow row)
    {
        var participantType = CompetitionResultRules.NormalizeParticipantType(row.ParticipantType);
        if (participantType is not ("Doubles" or "Timed Relay")) return null;

        var stage = CompetitionResultRules.NormalizeStage(row.Stage);
        var eventCode = CompetitionResultRules.NormalizeEvent(row.EventCode);
        var teamCode = row.TeamCode?.Trim();
        if (stage is null || eventCode is null || string.IsNullOrWhiteSpace(teamCode)) return null;
        if (participantType == "Timed Relay" && eventCode != "3-6-3") return null;

        var teamMap = participantType == "Doubles"
            ? memberships.Doubles
            : memberships.TimedRelays;
        if (!teamMap.TryGetValue(teamCode, out var members) || members.Count == 0) return null;
        if (!members.Any(member => string.Equals(member, linked.ParticipantCode, StringComparison.OrdinalIgnoreCase)))
            return null;

        var hasExternalPartner = participantType == "Doubles" && members.Count == 1;
        var membershipSha256 = MembershipSha256(participantType, teamCode, members, hasExternalPartner);

        var performance = ReadPerformance(row.AttemptsJson, row.Penalty);
        var logicalKey = string.Join(
            "\u001f",
            participantType.ToUpperInvariant(),
            teamCode.ToUpperInvariant(),
            stage.ToUpperInvariant(),
            eventCode.ToUpperInvariant());

        return new TeamCareerCandidate(
            logicalKey,
            participantType,
            teamCode,
            stage,
            eventCode,
            performance.Status,
            performance.OfficialBestTime,
            performance.RawBestTime,
            performance.AppliedPenalty,
            members.Count,
            hasExternalPartner,
            row.Revision,
            membershipSha256);
    }

    private static TeamPerformance ReadPerformance(string attemptsJson, decimal penalty)
    {
        if (penalty >= 999m)
            return new TeamPerformance("Scratch", null, null, 0m);

        decimal[] attempts;
        try
        {
            attempts = JsonSerializer.Deserialize<decimal[]>(attemptsJson) ?? [];
        }
        catch (JsonException)
        {
            return new TeamPerformance("Invalid", null, null, 0m);
        }

        if (attempts.Length == 0)
            return new TeamPerformance("Missing", null, null, 0m);

        var validAttempts = attempts
            .Where(value => value > 0m && value < 999m)
            .ToArray();
        if (validAttempts.Length > 0)
        {
            var rawBestTime = validAttempts.Min();
            var appliedPenalty = penalty > 0m && penalty < 999m ? penalty : 0m;
            return new TeamPerformance(
                "Valid",
                rawBestTime + appliedPenalty,
                rawBestTime,
                appliedPenalty);
        }

        return attempts.All(value => value == 999m)
            ? new TeamPerformance("Scratch", null, null, 0m)
            : new TeamPerformance("Invalid", null, null, 0m);
    }

    private static string MembershipSha256(
        string participantType,
        string teamCode,
        IReadOnlyList<string> members,
        bool hasExternalPartner)
    {
        var components = new List<string>
        {
            MembershipEvidenceVersion,
            participantType.Trim().ToUpperInvariant(),
            teamCode.Trim().ToUpperInvariant(),
            hasExternalPartner ? "EXTERNAL-PARTNER" : "REGISTERED-MEMBERS-ONLY"
        };
        components.AddRange(members.Select(member => member.Trim().ToUpperInvariant()));
        var payload = string.Join("\n", components);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private static int TeamTypeSort(string participantType) => participantType switch
    {
        "Doubles" => 0,
        "Timed Relay" => 1,
        _ => int.MaxValue
    };

    private static int StageSort(string stage) => stage switch
    {
        "Prelims" => 0,
        "Finals" => 1,
        _ => int.MaxValue
    };

    private static int EventSort(string eventCode) => eventCode switch
    {
        "3-3-3" => 0,
        "3-6-3" => 1,
        "Cycle" => 2,
        _ => int.MaxValue
    };

    private static InvalidOperationException Blocked(string code, string detail) =>
        new($"{code}: {detail}");

    private sealed record LinkedCompetitionRow(
        int CompetitionId,
        string CompetitionKey,
        string CompetitionName,
        DateOnly CompetitionDate,
        long ResultsRevision,
        string ParticipantCode);

    private sealed record TeamStateRow(
        string CompetitionKey,
        string JsonData,
        long StateRevision);

    private sealed record TeamResultRow(
        int CompetitionId,
        long ResultId,
        string Stage,
        string ParticipantType,
        string TeamCode,
        string EventCode,
        string AttemptsJson,
        decimal Penalty,
        long Revision);

    private sealed record TeamCareerCandidate(
        string LogicalKey,
        string ParticipantType,
        string TeamCode,
        string Stage,
        string EventCode,
        string ResultStatus,
        decimal? OfficialBestTime,
        decimal? RawBestTime,
        decimal AppliedPenalty,
        int RegisteredMemberCount,
        bool HasExternalPartner,
        long ResultRevision,
        string MembershipSha256);

    private sealed record TeamPerformance(
        string Status,
        decimal? OfficialBestTime,
        decimal? RawBestTime,
        decimal AppliedPenalty);
}
