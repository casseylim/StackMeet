using System.Text.Json;
using StackMeet.Api.Activities.SportStacking.Identity;
using StackMeet.Api.Activities.SportStacking.Ranking;

var failures = new List<string>();

void Assert(bool condition, string message)
{
    if (!condition) failures.Add(message);
}

void ExpectBlocked(Action action, string blocker, string message)
{
    try
    {
        action();
        failures.Add(message + " (no exception)");
    }
    catch (InvalidOperationException ex)
    {
        if (!ex.Message.StartsWith(blocker + ":", StringComparison.Ordinal))
            failures.Add(message + $" (wrong blocker: {ex.Message})");
    }
}

IdentityLinkedFinalsPlacementEvidence Evidence(
    string projectionVersion = FinalsHistoricalPlacementProjectionService.ProjectionVersion,
    string ruleVersion = FinalsRankingRuleVersions.GovernedFinalsV2,
    string snapshotSchemaVersion = FinalsRankingGovernanceService.GovernedV2SnapshotSchemaVersion) =>
    new(
        projectionVersion,
        ruleVersion,
        snapshotSchemaVersion,
        "sp4h-event-finals-v1",
        new string('A', 64),
        10,
        20,
        new DateTime(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc));

IdentityLinkedFinalsPlacementCareerPoint Point(
    FinalsHistoricalPlacementScope scope,
    string status = "Valid",
    decimal? officialBest = 5.123m,
    int? placement = 1,
    bool sharesPlacement = false,
    IdentityLinkedFinalsPlacementEvidence? evidence = null) =>
    new(
        "MERDEKA-2026",
        "Merdeka Sport Stacking Championship",
        new DateOnly(2026, 8, 31),
        scope,
        status,
        officialBest,
        placement,
        sharesPlacement,
        evidence ?? Evidence());

IdentityLinkedFinalsPlacementCareerReadModel Model(params IdentityLinkedFinalsPlacementCareerPoint[] points) =>
    new("NDT-2345678", points);

var safeScope = new FinalsHistoricalPlacementScope(
    "Individual",
    "12 & Under Male",
    "Cycle",
    "mixed",
    "all");

var publication = PublicFinalsPlacementPublicationContract.Create(Model(Point(safeScope, sharesPlacement: true)));
Assert(publication.PublicationVersion == PublicFinalsPlacementPublicationContract.PublicationVersion,
    "Publication version must be explicit and stable.");
Assert(publication.CohortPolicy == "Competition-time division · mixed category · no additional gender filter",
    "Cohort policy must describe the privacy-safe canonical scope accurately.");
Assert(publication.History.Count == 1, "Safe mixed/all SP-4L point should publish exactly once.");
Assert(publication.History[0].Placement == 1 && publication.History[0].SharesPlacement,
    "Placement and factual shared-rank membership must survive publication projection.");
Assert(publication.History[0].EventCode == "Cycle", "Event must remain part of the public rank context.");

var json = JsonSerializer.Serialize(publication);
Assert(!json.Contains("12 & Under Male", StringComparison.Ordinal),
    "Raw historical division labels must not leak into permanent public placement JSON.");
Assert(!json.Contains("SnapshotSha", StringComparison.OrdinalIgnoreCase),
    "Immutable snapshot internals must not leak into public placement JSON.");
Assert(!json.Contains("ParticipantCode", StringComparison.OrdinalIgnoreCase),
    "Participant code must not leak into public placement JSON.");
Assert(!json.Contains("Category", StringComparison.OrdinalIgnoreCase),
    "Raw category fields must not be published; policy is fixed by the contract.");
Assert(!json.Contains("Gender", StringComparison.OrdinalIgnoreCase),
    "Raw gender fields must not be published; policy is fixed by the contract.");

var pointProperties = typeof(PublicFinalsPlacementPoint)
    .GetProperties()
    .Select(item => item.Name)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
foreach (var forbidden in new[]
{
    "Division", "Category", "Gender", "Scope", "Evidence", "ParticipantCode", "ParticipantName",
    "StackerId", "CompetitionId", "ResultPublicId", "BirthDate", "Email", "Phone", "WssaId",
    "Medal", "Award", "Podium", "Record"
})
{
    Assert(!pointProperties.Contains(forbidden), $"Public placement point must not expose {forbidden}.");
}

ExpectBlocked(
    () => PublicFinalsPlacementPublicationContract.Create(Model(Point(safeScope with { Gender = "M" }))),
    PublicFinalsPlacementPublicationBlockers.ScopeNotPublicationSafe,
    "Male-only scope must fail closed instead of revealing gender-scoped permanent placement.");

ExpectBlocked(
    () => PublicFinalsPlacementPublicationContract.Create(Model(Point(safeScope with { Category = "normal" }))),
    PublicFinalsPlacementPublicationBlockers.ScopeNotPublicationSafe,
    "Normal-only scope must fail closed instead of revealing Special-status inference.");

ExpectBlocked(
    () => PublicFinalsPlacementPublicationContract.Create(Model(Point(safeScope with { Division = "all" }))),
    PublicFinalsPlacementPublicationBlockers.ScopeNotPublicationSafe,
    "Unscoped division placement must fail closed.");

ExpectBlocked(
    () => PublicFinalsPlacementPublicationContract.Create(Model(Point(
        safeScope,
        evidence: Evidence(projectionVersion: "wrong-projection")))),
    PublicFinalsPlacementPublicationBlockers.EvidenceNotPublicationSafe,
    "Non-SP-4K provenance must fail closed.");

ExpectBlocked(
    () => PublicFinalsPlacementPublicationContract.Create(Model(Point(
        safeScope,
        status: "Scratch",
        officialBest: null,
        placement: 2))),
    PublicFinalsPlacementPublicationBlockers.EvidenceNotPublicationSafe,
    "Scratch must never publish a fabricated placement.");

ExpectBlocked(
    () => PublicFinalsPlacementPublicationContract.Create(Model(Point(
        safeScope,
        status: "Valid",
        officialBest: 5.123m,
        placement: null))),
    PublicFinalsPlacementPublicationBlockers.EvidenceNotPublicationSafe,
    "Valid immutable placement evidence must carry a positive placement.");

var scratchPublication = PublicFinalsPlacementPublicationContract.Create(Model(Point(
    safeScope,
    status: "Scratch",
    officialBest: null,
    placement: null,
    sharesPlacement: false)));
Assert(scratchPublication.History.Single().Placement is null,
    "Scratch may remain a public Finals fact but must stay unplaced.");
Assert(scratchPublication.History.Single().OfficialBestTime is null,
    "Scratch must not publish an official Finals time.");

if (failures.Count > 0)
{
    Console.Error.WriteLine("SP-4M public Finals placement contract tests FAILED:");
    foreach (var failure in failures) Console.Error.WriteLine("- " + failure);
    return 1;
}

Console.WriteLine("SP-4M public Finals placement contract tests passed.");
return 0;
