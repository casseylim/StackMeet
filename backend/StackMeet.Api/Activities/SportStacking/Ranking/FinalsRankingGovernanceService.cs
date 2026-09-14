using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using StackMeet.Api.Activities;
using StackMeet.Api.Data;
using StackMeet.Api.Models;

namespace StackMeet.Api.Activities.SportStacking.Ranking;

public static class FinalsRankingRuleVersions
{
    public const string LegacyFinalsV1 = "legacy-finals-v1";
    public const string GovernedFinalsV2 = "governed-finals-v2";

    public static string ResolveStored(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return LegacyFinalsV1;
        return NormalizeExplicit(value)
            ?? throw new InvalidOperationException($"Unsupported Finals ranking rule version: {value.Trim()}.");
    }

    public static string? NormalizeExplicit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim().ToLowerInvariant() switch
        {
            LegacyFinalsV1 => LegacyFinalsV1,
            GovernedFinalsV2 => GovernedFinalsV2,
            _ => null
        };
    }
}

public sealed record FinalsRankingGovernanceRecord(
    int CompetitionId,
    string RuleVersion,
    DateTime RuleSelectedAt,
    int? RuleSelectedByUserId,
    string? SnapshotSchemaVersion,
    long? SourceStateRevision,
    long? SourceResultsRevision,
    string? SnapshotJson,
    string? SnapshotSha256,
    DateTime? SnapshotCapturedAt,
    int? SnapshotCapturedByUserId)
{
    public bool HasSnapshot => SnapshotCapturedAt is not null;
}

public sealed record FinalsRankingEffectiveRule(
    int CompetitionId,
    string RuleVersion,
    bool ExplicitSelection);

/// <summary>
/// Data-layer boundary for selecting a Finals ranking policy and freezing the source evidence
/// used by a finalized Sport Stacking competition.
/// </summary>
/// <remarks>
/// SP-4G intentionally has no controller/UI wiring for rule selection or capture. SP-4H adds a
/// read-only effective-rule projection through this service while keeping activity-specific
/// eligibility inside the Sport Stacking ranking boundary.
/// </remarks>
public sealed class FinalsRankingGovernanceService(StackMeetDbContext database)
{
    public const string SnapshotSchemaVersion = "finals-ranking-source-v1";

    static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public Task<FinalsRankingGovernanceRecord?> GetAsync(int competitionId, CancellationToken ct = default) =>
        ReadGovernanceAsync(competitionId, forUpdate: false, ct);

    public async Task<FinalsRankingEffectiveRule?> TryGetEffectiveRuleAsync(
        int competitionId,
        CancellationToken ct = default)
    {
        var competition = await database.Competitions
            .AsNoTracking()
            .Where(item => item.Id == competitionId)
            .Select(item => new { item.Id, item.ActivityModuleCode })
            .SingleOrDefaultAsync(ct);

        if (competition is null || !IsSportStackingCompetition(competition.ActivityModuleCode))
            return null;

        var governance = await ReadGovernanceAsync(competitionId, forUpdate: false, ct);
        return new FinalsRankingEffectiveRule(
            competition.Id,
            FinalsRankingRuleVersions.ResolveStored(governance?.RuleVersion),
            governance is not null);
    }

    public async Task<FinalsRankingGovernanceRecord> SelectRuleVersionAsync(
        int competitionId,
        string ruleVersion,
        int? actorUserId,
        CancellationToken ct = default)
    {
        var normalizedVersion = FinalsRankingRuleVersions.NormalizeExplicit(ruleVersion)
            ?? throw new ArgumentException("Finals ranking rule version must be an explicitly supported version.", nameof(ruleVersion));

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var competition = await LockCompetitionAsync(competitionId, ct)
            ?? throw new InvalidOperationException("Competition does not exist.");
        EnsureSportStackingCompetition(competition);
        if (!IsRuleSelectionOpen(competition))
            throw new InvalidOperationException("Finals ranking rule selection is allowed only while the competition is Draft or Active and not archived.");

        var existing = await ReadGovernanceAsync(competitionId, forUpdate: true, ct);
        if (existing?.HasSnapshot == true)
            throw new InvalidOperationException("Finals ranking rule cannot change after the finalized ranking source snapshot has been captured.");

        var selectedAt = DateTime.UtcNow;
        if (existing is null)
        {
            await database.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO [dbo].[FinalsRankingGovernance]
    ([CompetitionId], [RuleVersion], [RuleSelectedAt], [RuleSelectedByUserId])
VALUES
    ({competitionId}, {normalizedVersion}, {selectedAt}, {actorUserId});", ct);
        }
        else
        {
            await database.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE [dbo].[FinalsRankingGovernance]
SET [RuleVersion] = {normalizedVersion},
    [RuleSelectedAt] = {selectedAt},
    [RuleSelectedByUserId] = {actorUserId}
WHERE [CompetitionId] = {competitionId}
  AND [SnapshotCapturedAt] IS NULL;", ct);
        }

        var selected = await ReadGovernanceAsync(competitionId, forUpdate: true, ct)
            ?? throw new InvalidOperationException("Finals ranking governance selection was not persisted.");
        await transaction.CommitAsync(ct);
        return selected;
    }

    public async Task<FinalsRankingGovernanceRecord> CaptureFinalizedSnapshotAsync(
        int competitionId,
        int? actorUserId,
        CancellationToken ct = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var competition = await LockCompetitionAsync(competitionId, ct)
            ?? throw new InvalidOperationException("Competition does not exist.");
        EnsureSportStackingCompetition(competition);
        if (!IsFinalized(competition))
            throw new InvalidOperationException("Finals ranking source snapshot can be captured only after the competition is Closed or Archived.");

        var governance = await ReadGovernanceAsync(competitionId, forUpdate: true, ct);
        if (governance?.HasSnapshot == true)
            throw new InvalidOperationException("Finals ranking source snapshot is already captured and immutable.");

        var ruleVersion = governance is null
            ? FinalsRankingRuleVersions.LegacyFinalsV1
            : FinalsRankingRuleVersions.ResolveStored(governance.RuleVersion);

        if (ruleVersion == FinalsRankingRuleVersions.GovernedFinalsV2)
        {
            throw new InvalidOperationException(
                "governed-finals-v2 snapshot capture is blocked until a later phase makes the operator Finals engine version-aware and proves v2 was actually applied.");
        }

        var state = await LockCompetitionStateAsync(competition.CompetitionKey, ct)
            ?? throw new InvalidOperationException("CompetitionState is required to preserve the authoritative competition-time division snapshot.");
        var stateRoot = ParseStateObject(state.JsonData);

        var finalsResults = (await database.CompetitionResults
                .AsNoTracking()
                .Where(item => item.CompetitionId == competition.Id && item.Stage == "Finals")
                .ToListAsync(ct))
            .OrderBy(item => item.ParticipantType, StringComparer.Ordinal)
            .ThenBy(item => item.ParticipantCode, StringComparer.Ordinal)
            .ThenBy(item => item.EventCode, StringComparer.Ordinal)
            .ThenBy(item => item.PublicId)
            .Select(item => new FinalsSnapshotResult(
                item.PublicId,
                item.ParticipantType,
                item.ParticipantCode,
                item.EventCode,
                item.AttemptsJson,
                item.Penalty,
                item.Revision))
            .ToArray();

        var envelope = new FinalsRankingSourceSnapshot(
            SnapshotSchemaVersion,
            ruleVersion,
            new FinalsSnapshotCompetition(
                competition.CompetitionCode,
                competition.CompetitionKey,
                string.IsNullOrWhiteSpace(competition.ActivityModuleCode)
                    ? SportStackingActivityModule.ModuleCode
                    : competition.ActivityModuleCode.Trim(),
                competition.Status,
                competition.StartDate,
                competition.EndDate,
                state.StateRevision,
                competition.ResultsRevision),
            stateRoot,
            finalsResults);

        var snapshotJson = JsonSerializer.Serialize(envelope, SnapshotJsonOptions);
        var snapshotHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshotJson)));
        var capturedAt = DateTime.UtcNow;

        if (governance is null)
        {
            await database.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO [dbo].[FinalsRankingGovernance]
    ([CompetitionId], [RuleVersion], [RuleSelectedAt], [RuleSelectedByUserId],
     [SnapshotSchemaVersion], [SourceStateRevision], [SourceResultsRevision],
     [SnapshotJson], [SnapshotSha256], [SnapshotCapturedAt], [SnapshotCapturedByUserId])
VALUES
    ({competitionId}, {ruleVersion}, {capturedAt}, {actorUserId},
     {SnapshotSchemaVersion}, {state.StateRevision}, {competition.ResultsRevision},
     {snapshotJson}, {snapshotHash}, {capturedAt}, {actorUserId});", ct);
        }
        else
        {
            var affected = await database.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE [dbo].[FinalsRankingGovernance]
SET [SnapshotSchemaVersion] = {SnapshotSchemaVersion},
    [SourceStateRevision] = {state.StateRevision},
    [SourceResultsRevision] = {competition.ResultsRevision},
    [SnapshotJson] = {snapshotJson},
    [SnapshotSha256] = {snapshotHash},
    [SnapshotCapturedAt] = {capturedAt},
    [SnapshotCapturedByUserId] = {actorUserId}
WHERE [CompetitionId] = {competitionId}
  AND [SnapshotCapturedAt] IS NULL;", ct);
            if (affected != 1)
                throw new InvalidOperationException("Finals ranking source snapshot could not be captured exactly once.");
        }

        var captured = await ReadGovernanceAsync(competitionId, forUpdate: true, ct)
            ?? throw new InvalidOperationException("Finals ranking governance snapshot was not persisted.");
        await transaction.CommitAsync(ct);
        return captured;
    }

    static JsonElement ParseStateObject(string jsonData)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonData);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("CompetitionState must contain a JSON object before Finals ranking snapshot capture.");
            return document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("CompetitionState contains malformed JSON and cannot be used as ranking provenance.", ex);
        }
    }

    async Task<Competition?> LockCompetitionAsync(int competitionId, CancellationToken ct) =>
        await database.Competitions
            .FromSqlInterpolated($"SELECT * FROM [dbo].[Competition] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {competitionId}")
            .SingleOrDefaultAsync(ct);

    async Task<CompetitionState?> LockCompetitionStateAsync(string competitionKey, CancellationToken ct) =>
        await database.CompetitionStates
            .FromSqlInterpolated($"SELECT * FROM [dbo].[CompetitionState] WITH (UPDLOCK, HOLDLOCK) WHERE [CompetitionKey] = {competitionKey}")
            .SingleOrDefaultAsync(ct);

    static bool IsRuleSelectionOpen(Competition competition) =>
        competition.ArchivedAt is null && competition.Status is "Draft" or "Active";

    static bool IsFinalized(Competition competition) =>
        competition.ArchivedAt is not null || competition.Status is "Closed" or "Archived";

    static bool IsSportStackingCompetition(string? activityModuleCode) =>
        string.IsNullOrWhiteSpace(activityModuleCode)
        || activityModuleCode.Equals(SportStackingActivityModule.ModuleCode, StringComparison.OrdinalIgnoreCase);

    static void EnsureSportStackingCompetition(Competition competition)
    {
        if (!IsSportStackingCompetition(competition.ActivityModuleCode))
        {
            throw new InvalidOperationException("Finals ranking governance currently applies only to the Sport Stacking activity module.");
        }
    }

    async Task<FinalsRankingGovernanceRecord?> ReadGovernanceAsync(int competitionId, bool forUpdate, CancellationToken ct)
    {
        var connection = database.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await database.Database.OpenConnectionAsync(ct);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = database.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $@"
SELECT [CompetitionId], [RuleVersion], [RuleSelectedAt], [RuleSelectedByUserId],
       [SnapshotSchemaVersion], [SourceStateRevision], [SourceResultsRevision],
       [SnapshotJson], [SnapshotSha256], [SnapshotCapturedAt], [SnapshotCapturedByUserId]
FROM [dbo].[FinalsRankingGovernance]{(forUpdate ? " WITH (UPDLOCK, HOLDLOCK)" : string.Empty)}
WHERE [CompetitionId] = @competitionId;";
            AddParameter(command, "@competitionId", competitionId);

            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            return new FinalsRankingGovernanceRecord(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetDateTime(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetInt64(5),
                reader.IsDBNull(6) ? null : reader.GetInt64(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                reader.IsDBNull(10) ? null : reader.GetInt32(10));
        }
        finally
        {
            if (openedHere) await database.Database.CloseConnectionAsync();
        }
    }

    static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    sealed record FinalsRankingSourceSnapshot(
        string SchemaVersion,
        string RuleVersion,
        FinalsSnapshotCompetition Competition,
        JsonElement CompetitionState,
        IReadOnlyList<FinalsSnapshotResult> FinalsResults);

    sealed record FinalsSnapshotCompetition(
        string CompetitionCode,
        string CompetitionKey,
        string ActivityModuleCode,
        string Status,
        DateOnly StartDate,
        DateOnly EndDate,
        long StateRevision,
        long ResultsRevision);

    sealed record FinalsSnapshotResult(
        Guid PublicId,
        string ParticipantType,
        string ParticipantCode,
        string EventCode,
        string AttemptsJson,
        decimal Penalty,
        long Revision);
}