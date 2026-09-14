using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;

namespace StackMeet.Api.Activities.SportStacking.Ranking;

public static class FinalsRankingCertificationBlockers
{
    public const string CompetitionNotFound = "competition-not-found";
    public const string ActivityNotSportStacking = "activity-not-sport-stacking";
    public const string CompetitionNotFinalized = "competition-not-finalized";
    public const string GovernedV2NotExplicitlySelected = "governed-finals-v2-not-explicitly-selected";
    public const string UnsupportedRuleVersion = "unsupported-rule-version";
    public const string SnapshotAlreadyCaptured = "snapshot-already-captured";
    public const string CompetitionStateMissing = "competition-state-missing";
    public const string CompetitionStateMalformed = "competition-state-malformed";
    public const string StateRevisionInvalid = "state-revision-invalid";
    public const string ResultRevisionInconsistent = "result-revision-inconsistent";
    public const string ResultAttemptsMalformed = "result-attempts-malformed";
}

public sealed record FinalsRankingCertificationReadiness(
    int CompetitionId,
    string? RuleVersion,
    string OperatorContractVersion,
    bool ExplicitRuleSelection,
    bool SnapshotAlreadyCaptured,
    bool IsReady,
    IReadOnlyList<string> BlockingReasons,
    long? SourceStateRevision,
    long? SourceResultsRevision,
    int FinalsResultCount);

/// <summary>
/// SP-4I advisory boundary that proves whether a finalized Sport Stacking competition has
/// sufficient durable source evidence to certify a governed-finals-v2 ranking snapshot later.
/// It does not capture a snapshot, publish placement, or mutate ranking governance.
/// </summary>
public sealed class FinalsRankingCertificationReadinessService(StackMeetDbContext database)
{
    /// <summary>
    /// Identifies the reviewed operator contract that consumes the persisted Finals rule.
    /// SP-4H established this contract for event-level Finals while leaving Prelims and
    /// All-Around on their existing legacy-compatible paths.
    /// </summary>
    public const string OperatorContractVersion = "sp4h-event-finals-v1";

    public async Task<FinalsRankingCertificationReadiness> AssessAsync(
        int competitionId,
        CancellationToken ct = default)
    {
        var blockers = new List<string>();
        var competition = await database.Competitions
            .AsNoTracking()
            .Where(item => item.Id == competitionId)
            .Select(item => new
            {
                item.Id,
                item.CompetitionKey,
                item.ActivityModuleCode,
                item.Status,
                item.ArchivedAt,
                item.ResultsRevision
            })
            .SingleOrDefaultAsync(ct);

        if (competition is null)
        {
            blockers.Add(FinalsRankingCertificationBlockers.CompetitionNotFound);
            return Build(
                competitionId,
                ruleVersion: null,
                explicitRuleSelection: false,
                snapshotAlreadyCaptured: false,
                blockers,
                sourceStateRevision: null,
                sourceResultsRevision: null,
                finalsResultCount: 0);
        }

        if (!IsSportStackingCompetition(competition.ActivityModuleCode))
            blockers.Add(FinalsRankingCertificationBlockers.ActivityNotSportStacking);

        if (!IsFinalized(competition.Status, competition.ArchivedAt))
            blockers.Add(FinalsRankingCertificationBlockers.CompetitionNotFinalized);

        var governance = await new FinalsRankingGovernanceService(database).GetAsync(competition.Id, ct);
        string? ruleVersion = null;
        try
        {
            ruleVersion = FinalsRankingRuleVersions.ResolveStored(governance?.RuleVersion);
        }
        catch (InvalidOperationException)
        {
            blockers.Add(FinalsRankingCertificationBlockers.UnsupportedRuleVersion);
        }

        var explicitGovernedV2 = governance is not null
            && string.Equals(ruleVersion, FinalsRankingRuleVersions.GovernedFinalsV2, StringComparison.Ordinal);
        if (!explicitGovernedV2)
            blockers.Add(FinalsRankingCertificationBlockers.GovernedV2NotExplicitlySelected);

        if (governance?.HasSnapshot == true)
            blockers.Add(FinalsRankingCertificationBlockers.SnapshotAlreadyCaptured);

        long? stateRevision = null;
        var state = await database.CompetitionStates
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.CompetitionKey == competition.CompetitionKey, ct);
        if (state is null)
        {
            blockers.Add(FinalsRankingCertificationBlockers.CompetitionStateMissing);
        }
        else
        {
            stateRevision = state.StateRevision;
            if (state.StateRevision <= 0)
                blockers.Add(FinalsRankingCertificationBlockers.StateRevisionInvalid);
            if (!IsJsonObject(state.JsonData))
                blockers.Add(FinalsRankingCertificationBlockers.CompetitionStateMalformed);
        }

        var finalsResults = await database.CompetitionResults
            .AsNoTracking()
            .Where(item => item.CompetitionId == competition.Id && item.Stage == "Finals")
            .Select(item => new { item.Revision, item.AttemptsJson })
            .ToListAsync(ct);

        if (finalsResults.Any(item => item.Revision <= 0 || item.Revision > competition.ResultsRevision))
            blockers.Add(FinalsRankingCertificationBlockers.ResultRevisionInconsistent);

        if (finalsResults.Any(item => !IsNumericJsonArray(item.AttemptsJson)))
            blockers.Add(FinalsRankingCertificationBlockers.ResultAttemptsMalformed);

        return Build(
            competition.Id,
            ruleVersion,
            explicitRuleSelection: governance is not null,
            snapshotAlreadyCaptured: governance?.HasSnapshot == true,
            blockers,
            stateRevision,
            competition.ResultsRevision,
            finalsResults.Count);
    }

    static FinalsRankingCertificationReadiness Build(
        int competitionId,
        string? ruleVersion,
        bool explicitRuleSelection,
        bool snapshotAlreadyCaptured,
        List<string> blockers,
        long? sourceStateRevision,
        long? sourceResultsRevision,
        int finalsResultCount) =>
        new(
            competitionId,
            ruleVersion,
            OperatorContractVersion,
            explicitRuleSelection,
            snapshotAlreadyCaptured,
            blockers.Count == 0,
            blockers.ToArray(),
            sourceStateRevision,
            sourceResultsRevision,
            finalsResultCount);

    static bool IsFinalized(string status, DateTime? archivedAt) =>
        archivedAt is not null || status is "Closed" or "Archived";

    static bool IsSportStackingCompetition(string? activityModuleCode) =>
        string.IsNullOrWhiteSpace(activityModuleCode)
        || activityModuleCode.Equals(SportStackingActivityModule.ModuleCode, StringComparison.OrdinalIgnoreCase);

    static bool IsJsonObject(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    static bool IsNumericJsonArray(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return false;
            foreach (var value in document.RootElement.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out _)) return false;
            }
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
