using System.Text.Json;
using StackMeet.Api.Activities.SportStacking.Identity;

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

IdentityLinkedTeamCareerEvidence Evidence(
    long stateRevision = 11,
    long resultsRevision = 22,
    long resultRevision = 7,
    string? sha256 = null) =>
    new(
        stateRevision,
        resultsRevision,
        resultRevision,
        sha256 ?? new string('A', 64));

IdentityLinkedTeamCareerPoint Point(
    string participantType = "Doubles",
    string teamCode = "D1",
    string stage = "Finals",
    string eventCode = "Cycle",
    string status = "Valid",
    decimal? officialBest = 8.500m,
    decimal? rawBest = 8.000m,
    decimal appliedPenalty = 0.500m,
    int registeredMemberCount = 2,
    bool hasExternalPartner = false,
    IdentityLinkedTeamCareerEvidence? evidence = null,
    string competitionKey = "MERDEKA-2026") =>
    new(
        competitionKey,
        "Merdeka Sport Stacking Championship",
        new DateOnly(2026, 8, 31),
        participantType,
        teamCode,
        stage,
        eventCode,
        status,
        officialBest,
        rawBest,
        appliedPenalty,
        registeredMemberCount,
        hasExternalPartner,
        evidence ?? Evidence());

IdentityLinkedTeamCareerReadModel Model(
    string nadiTrackId = "NDT-2345678",
    params IdentityLinkedTeamCareerPoint[] points) =>
    new(nadiTrackId, points);

var publication = PublicTeamCareerPublicationContract.Create(Model(points: [Point()]));
Assert(publication.PublicationVersion == PublicTeamCareerPublicationContract.PublicationVersion,
    "Publication version must be explicit and stable.");
Assert(publication.PrivacyPolicy == "Verified team membership · teammate identities withheld",
    "Public contract must state the reviewed team privacy policy.");
Assert(publication.History.Count == 1,
    "Safe SP-4Q team point should publish exactly once.");

var doubles = publication.History.Single();
Assert(doubles.ParticipantType == "Doubles"
    && doubles.Stage == "Finals"
    && doubles.EventCode == "Cycle",
    "Doubles type/stage/event context must survive publication.");
Assert(doubles.RawBestTime == 8.000m
    && doubles.AppliedPenalty == 0.500m
    && doubles.OfficialBestTime == 8.500m,
    "Factual team timing and penalty must survive publication.");

var childParent = PublicTeamCareerPublicationContract.Create(Model(
    points:
    [
        Point(
            teamCode: "CP1",
            registeredMemberCount: 1,
            hasExternalPartner: true)
    ]));
Assert(childParent.History.Count == 1 && childParent.History[0].ParticipantType == "Doubles",
    "Child/Parent may publish the registered athlete's team performance without publishing family linkage.");

var relay = PublicTeamCareerPublicationContract.Create(Model(
    points:
    [
        Point(
            participantType: "Timed Relay",
            teamCode: "R1",
            eventCode: "3-6-3",
            officialBest: 20.000m,
            rawBest: 20.000m,
            appliedPenalty: 0m,
            registeredMemberCount: 4)
    ]));
Assert(relay.History.Single().ParticipantType == "Timed Relay"
    && relay.History.Single().EventCode == "3-6-3",
    "Timed Relay 3-6-3 may publish as factual team history.");

var scratch = PublicTeamCareerPublicationContract.Create(Model(
    points:
    [
        Point(
            status: "scratch",
            officialBest: null,
            rawBest: null,
            appliedPenalty: 0m)
    ]));
Assert(scratch.History.Single().ResultStatus == "Scratch"
    && scratch.History.Single().OfficialBestTime is null,
    "Scratch remains factual public team history without a fabricated time.");

var json = JsonSerializer.Serialize(childParent);
foreach (var forbiddenText in new[]
{
    "\"TeamCode\"",
    "\"RegisteredMemberCount\"",
    "\"HasExternalPartner\"",
    "\"Evidence\"",
    "\"RegisteredMembershipSha256\"",
    "\"CompetitionStateRevision\"",
    "\"CompetitionResultsRevision\"",
    "\"ResultRevision\""
})
{
    Assert(!json.Contains(forbiddenText, StringComparison.OrdinalIgnoreCase),
        $"Public team career JSON must not expose {forbiddenText}.");
}

var pointProperties = typeof(PublicTeamCareerPoint)
    .GetProperties()
    .Select(item => item.Name)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
foreach (var forbidden in new[]
{
    "TeamCode", "RegisteredMemberCount", "HasExternalPartner", "Evidence",
    "RegisteredMembershipSha256", "CompetitionStateRevision", "CompetitionResultsRevision", "ResultRevision",
    "ParticipantCode", "ParticipantName", "MemberParticipantCodes", "MemberNames", "PartnerName", "ParentName",
    "StackerId", "CompetitionId", "ResultPublicId", "BirthDate", "Email", "Phone", "WssaId",
    "Placement", "Rank", "Medal", "Award", "Podium", "Record"
})
{
    Assert(!pointProperties.Contains(forbidden), $"Public team career point must not expose {forbidden}.");
}

ExpectBlocked(
    () => PublicTeamCareerPublicationContract.Create(Model("not-a-naditrack-id", Point())),
    PublicTeamCareerPublicationBlockers.SourceNotPublicationSafe,
    "Malformed permanent identity must fail closed.");

ExpectBlocked(
    () => PublicTeamCareerPublicationContract.Create(Model(points: [Point(participantType: "Individual")])),
    PublicTeamCareerPublicationBlockers.SourceNotPublicationSafe,
    "Individual result cannot enter the team publication contract.");

ExpectBlocked(
    () => PublicTeamCareerPublicationContract.Create(Model(points: [Point(stage: "Awards")])),
    PublicTeamCareerPublicationBlockers.SourceNotPublicationSafe,
    "Unsupported team stage must fail closed.");

ExpectBlocked(
    () => PublicTeamCareerPublicationContract.Create(Model(
        points:
        [
            Point(
                participantType: "Timed Relay",
                teamCode: "R1",
                eventCode: "Cycle",
                registeredMemberCount: 4)
        ])),
    PublicTeamCareerPublicationBlockers.SourceNotPublicationSafe,
    "Timed Relay must remain restricted to 3-6-3.");

ExpectBlocked(
    () => PublicTeamCareerPublicationContract.Create(Model(
        points:
        [
            Point(
                participantType: "Timed Relay",
                teamCode: "R1",
                eventCode: "3-6-3",
                registeredMemberCount: 4,
                hasExternalPartner: true)
        ])),
    PublicTeamCareerPublicationBlockers.SourceNotPublicationSafe,
    "Timed Relay cannot publish an external-partner shape.");

ExpectBlocked(
    () => PublicTeamCareerPublicationContract.Create(Model(
        points:
        [
            Point(
                registeredMemberCount: 1,
                hasExternalPartner: false)
        ])),
    PublicTeamCareerPublicationBlockers.SourceNotPublicationSafe,
    "One registered Doubles member requires an external partner internally.");

ExpectBlocked(
    () => PublicTeamCareerPublicationContract.Create(Model(
        points:
        [
            Point(
                registeredMemberCount: 2,
                hasExternalPartner: true)
        ])),
    PublicTeamCareerPublicationBlockers.SourceNotPublicationSafe,
    "Two registered Doubles members cannot also publish an external-partner source shape.");

ExpectBlocked(
    () => PublicTeamCareerPublicationContract.Create(Model(
        points:
        [
            Point(evidence: Evidence(sha256: "NOT-A-SHA256"))
        ])),
    PublicTeamCareerPublicationBlockers.EvidenceNotPublicationSafe,
    "Malformed registered-membership fingerprint must fail closed.");

ExpectBlocked(
    () => PublicTeamCareerPublicationContract.Create(Model(
        points:
        [
            Point(evidence: Evidence(resultRevision: 0))
        ])),
    PublicTeamCareerPublicationBlockers.EvidenceNotPublicationSafe,
    "Non-positive result revision must fail closed.");

ExpectBlocked(
    () => PublicTeamCareerPublicationContract.Create(Model(
        points:
        [
            Point(officialBest: 8.600m, rawBest: 8.000m, appliedPenalty: 0.500m)
        ])),
    PublicTeamCareerPublicationBlockers.EvidenceNotPublicationSafe,
    "Valid public timing must equal raw best plus applied penalty.");

ExpectBlocked(
    () => PublicTeamCareerPublicationContract.Create(Model(
        points:
        [
            Point(
                status: "Scratch",
                officialBest: 8.000m,
                rawBest: 8.000m,
                appliedPenalty: 0m)
        ])),
    PublicTeamCareerPublicationBlockers.EvidenceNotPublicationSafe,
    "Scratch must never carry a fabricated team time.");

ExpectBlocked(
    () => PublicTeamCareerPublicationContract.Create(Model(
        points:
        [
            Point(teamCode: "D1"),
            Point(teamCode: "D2", officialBest: 9.000m, rawBest: 9.000m, appliedPenalty: 0m)
        ])),
    PublicTeamCareerPublicationBlockers.ContextAmbiguous,
    "Multiple internal team entries that collapse to the same privacy-safe public context must fail closed.");

if (failures.Count > 0)
{
    Console.Error.WriteLine("SP-4R public team career contract tests FAILED:");
    foreach (var failure in failures) Console.Error.WriteLine("- " + failure);
    return 1;
}

Console.WriteLine("SP-4R public team career publication contract tests passed.");
return 0;
