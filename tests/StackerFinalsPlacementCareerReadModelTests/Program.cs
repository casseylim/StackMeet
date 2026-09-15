using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Activities.SportStacking.Identity;
using StackMeet.Api.Activities.SportStacking.Ranking;
using StackMeet.Api.Data;
using StackMeet.Api.Models;

const string server = @"(localdb)\MSSQLLocalDB";
var databaseName = $"StackMeet_StackerFinalsPlacementSp4lTest_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
var builder = new SqlConnectionStringBuilder
{
    DataSource = server,
    InitialCatalog = databaseName,
    IntegratedSecurity = true,
    TrustServerCertificate = true,
    MultipleActiveResultSets = true,
    ConnectTimeout = 5
};

if (!string.Equals(builder.DataSource, server, StringComparison.Ordinal))
    throw new InvalidOperationException("Refusing SP-4L integration test: unauthorized SQL server.");
if (!databaseName.StartsWith("StackMeet_StackerFinalsPlacementSp4lTest_", StringComparison.Ordinal)
    || databaseName.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
    throw new InvalidOperationException("Refusing SP-4L cleanup: generated database name failed safety validation.");

Console.WriteLine($"SP-4L LocalDB harness: server={builder.DataSource}; database={builder.InitialCatalog}");
var options = new DbContextOptionsBuilder<StackMeetDbContext>()
    .UseSqlServer(builder.ConnectionString)
    .Options;

Exception? primaryFailure = null;
Exception? cleanupFailure = null;

try
{
    var masterBuilder = new SqlConnectionStringBuilder(builder.ConnectionString) { InitialCatalog = "master" };
    await using (var probe = new SqlConnection(masterBuilder.ConnectionString))
    {
        using var probeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await probe.OpenAsync(probeTimeout.Token);
    }

    await using var db = new StackMeetDbContext(options);
    using (var migrationTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
    {
        await db.Database.MigrateAsync(migrationTimeout.Token);
    }

    var now = DateTime.UtcNow;
    var publicIdentity = NewIdentity("NDT-ABCDEFG", "Career", "Stacker", true, now);
    var privateIdentity = NewIdentity("NDT-BCDEFGH", "Private", "Stacker", false, now);
    db.SportStackerIdentities.AddRange(publicIdentity, privateIdentity);

    var c1 = NewCompetition("SP4L-1", "January Placement", new DateOnly(2026, 1, 10), "Active", true, resultsRevision: 6, now);
    var c2 = NewCompetition("SP4L-2", "February Placement", new DateOnly(2026, 2, 10), "Active", true, resultsRevision: 2, now);
    var active = NewCompetition("SP4L-ACTIVE", "Active Placement", new DateOnly(2026, 3, 10), "Active", true, resultsRevision: 0, now);
    var hidden = NewCompetition("SP4L-HIDDEN", "Hidden Placement", new DateOnly(2026, 4, 10), "Closed", false, resultsRevision: 0, now);
    db.Competitions.AddRange(c1, c2, active, hidden);
    await db.SaveChangesAsync();

    var s1 = NewStacker(c1.Id, "A1", "Career", "Stacker", "M", now);
    var s2 = NewStacker(c2.Id, "A2", "Career", "Stacker", "F", now);
    var sa = NewStacker(active.Id, "AA", "Career", "Stacker", "M", now);
    var sh = NewStacker(hidden.Id, "AH", "Career", "Stacker", "M", now);
    var sameNameUnlinked = NewStacker(c1.Id, "B1", "Career", "Stacker", "M", now);
    db.Stackers.AddRange(s1, s2, sa, sh, sameNameUnlinked);
    await db.SaveChangesAsync();

    db.StackerIdentityLinks.AddRange(
        NewLink(publicIdentity.Id, s1.Id, "TEST_SP4L_C1", now),
        NewLink(publicIdentity.Id, s2.Id, "TEST_SP4L_C2", now),
        NewLink(publicIdentity.Id, sa.Id, "TEST_SP4L_ACTIVE", now),
        NewLink(publicIdentity.Id, sh.Id, "TEST_SP4L_HIDDEN", now));
    await db.SaveChangesAsync();

    db.CompetitionStates.AddRange(
        NewState(
            c1.CompetitionKey,
            """
            {
              "stackers": [
                { "id": "A1", "name": "Career Stacker", "gender": "M", "division": "Open", "special": "No" },
                { "id": "B1", "name": "Career Stacker", "gender": "M", "division": "Open", "special": "No" },
                { "id": "C1", "name": "Faster Female", "gender": "F", "division": "Open", "special": "No" },
                { "id": "S1", "name": "Special Athlete", "gender": "M", "division": "Open", "special": "Yes" }
              ]
            }
            """,
            revision: 11,
            now),
        NewState(
            c2.CompetitionKey,
            """
            {
              "stackers": [
                { "id": "A2", "name": "Career Stacker", "gender": "F", "division": "Senior", "special": "No" },
                { "id": "X2", "name": "Second Athlete", "gender": "F", "division": "Senior", "special": "No" }
              ]
            }
            """,
            revision: 12,
            now));

    var c1CareerCycle = NewResult(c1.Id, "A1", "Cycle", "[5.5,5.7,5.8]", 0m, 1, now);
    db.CompetitionResults.AddRange(
        c1CareerCycle,
        NewResult(c1.Id, "B1", "Cycle", "[5.0,5.1,5.2]", 0m, 2, now),
        NewResult(c1.Id, "C1", "Cycle", "[4.8,4.9,5.0]", 0m, 3, now),
        NewResult(c1.Id, "S1", "Cycle", "[4.0,4.1,4.2]", 0m, 4, now),
        NewResult(c1.Id, "A1", "3-3-3", "[3.0,3.1,3.2]", 999m, 5, now),
        NewResult(c1.Id, "B1", "3-3-3", "[6.0,6.1,6.2]", 0m, 6, now),
        NewResult(c2.Id, "A2", "Cycle", "[4.0,4.1,4.2]", 0m, 1, now),
        NewResult(c2.Id, "X2", "Cycle", "[4.2,4.3,4.4]", 0m, 2, now));
    await db.SaveChangesAsync();

    var governance = new FinalsRankingGovernanceService(db);
    await governance.SelectRuleVersionAsync(c1.Id, FinalsRankingRuleVersions.GovernedFinalsV2, 4101);
    await governance.SelectRuleVersionAsync(c2.Id, FinalsRankingRuleVersions.GovernedFinalsV2, 4102);

    c1.Status = "Closed";
    c2.Status = "Closed";
    c1.UpdatedAt = DateTime.UtcNow;
    c2.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    var c1Certified = await governance.CertifyGovernedV2SnapshotAsync(c1.Id, 4111);
    var c2Certified = await governance.CertifyGovernedV2SnapshotAsync(c2.Id, 4112);
    Assert(c1Certified.HasSnapshot && c2Certified.HasSnapshot, "governed-v2 immutable evidence is certified before SP-4L linkage");

    var selections = new[]
    {
        new IdentityLinkedFinalsPlacementSelection(
            c1.Id,
            new FinalsHistoricalPlacementScope("Individual", "Open", "Cycle", "normal", "all")),
        new IdentityLinkedFinalsPlacementSelection(
            c1.Id,
            new FinalsHistoricalPlacementScope("individual", "Open", "cycle", "NORMAL", "m")),
        new IdentityLinkedFinalsPlacementSelection(
            c1.Id,
            new FinalsHistoricalPlacementScope("Individual", "Open", "3-3-3", "normal", "all")),
        new IdentityLinkedFinalsPlacementSelection(
            c2.Id,
            new FinalsHistoricalPlacementScope("Individual", "Senior", "Cycle", "normal", "all")),
        new IdentityLinkedFinalsPlacementSelection(
            active.Id,
            new FinalsHistoricalPlacementScope("Individual", "Open", "Cycle", "normal", "all")),
        new IdentityLinkedFinalsPlacementSelection(
            hidden.Id,
            new FinalsHistoricalPlacementScope("Individual", "Open", "Cycle", "normal", "all")),
        new IdentityLinkedFinalsPlacementSelection(
            c1.Id,
            new FinalsHistoricalPlacementScope("Individual", "Open", "Cycle", "special", "all")),
        // Exact duplicate must not create a duplicate career fact.
        new IdentityLinkedFinalsPlacementSelection(
            c2.Id,
            new FinalsHistoricalPlacementScope("Individual", "Senior", "Cycle", "normal", "all"))
    };

    var service = new IdentityLinkedFinalsPlacementCareerService(db);
    var career = await service.GetPublicEligibleAsync(publicIdentity.NadiTrackId, selections)
        ?? throw new InvalidOperationException("Expected public SP-4L career read model.");

    Assert(career.NadiTrackId == publicIdentity.NadiTrackId, "career read model is keyed by permanent NADITrack ID");
    Assert(career.History.Count == 4, "only linked, finalized/public and in-scope immutable placements enter the career model");

    var c1All = Point(career, "SP4L-1", "Cycle", "normal", "all");
    var c1Male = Point(career, "SP4L-1", "Cycle", "normal", "M");
    Assert(c1All.Placement == 3 && c1Male.Placement == 2,
        "the same immutable Finals result keeps different placement under different explicit gender scopes");
    Assert(c1All.Scope.Division == "Open" && c1All.Scope.ParticipantType == "Individual",
        "placement retains explicit participant type and competition-snapshot division scope");

    var scratch = Point(career, "SP4L-1", "3-3-3", "normal", "all");
    Assert(scratch.ResultStatus == "scratch" && scratch.Placement is null && scratch.OfficialBestTime is null,
        "Scratch Finals appearance is retained without a fabricated placement or official time");

    var c2Cycle = Point(career, "SP4L-2", "Cycle", "normal", "all");
    Assert(c2Cycle.Placement == 1 && c2Cycle.Scope.Division == "Senior",
        "career read model supports a different explicit historical division in another competition");
    Assert(c2Cycle.Evidence.ProjectionVersion == FinalsHistoricalPlacementProjectionService.ProjectionVersion
        && c2Cycle.Evidence.RuleVersion == FinalsRankingRuleVersions.GovernedFinalsV2
        && !string.IsNullOrWhiteSpace(c2Cycle.Evidence.SnapshotSha256),
        "identity-linked placement retains immutable SP-4K evidence provenance");

    Assert(career.History.All(item => item.CompetitionKey != "SP4L-ACTIVE" && item.CompetitionKey != "SP4L-HIDDEN"),
        "active and non-public competitions cannot enter SP-4L even when identity links exist");
    Assert(career.History.All(item => item.Scope.Category != "special"),
        "an explicit cohort that excludes the linked athlete cannot fabricate a career placement");

    // The current registration row is only the reviewed identity-link bridge. Its mutable profile fields
    // and live result row are not historical ranking authority.
    s1.FirstName = "Mutated";
    s1.LastName = "Registration";
    s1.Gender = "F";
    s1.CustomDivision = "Changed";
    s1.UpdatedAt = DateTime.UtcNow;
    c1CareerCycle.AttemptsJson = "[1.0,1.1,1.2]";
    c1CareerCycle.Penalty = 0m;
    c1CareerCycle.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    var afterLiveMutation = await service.GetPublicEligibleAsync(publicIdentity.NadiTrackId, selections)
        ?? throw new InvalidOperationException("Expected public SP-4L career after live mutation.");
    Assert(Point(afterLiveMutation, "SP4L-1", "Cycle", "normal", "all").Placement == 3,
        "current Stacker/profile and CompetitionResult mutations cannot rewrite immutable historical placement");

    var privateCareer = await service.GetPublicEligibleAsync(privateIdentity.NadiTrackId, selections);
    Assert(privateCareer is null, "private identity shares the public not-found boundary");
    var malformedCareer = await service.GetPublicEligibleAsync("not-a-naditrack-id", selections);
    Assert(malformedCareer is null, "malformed identity shares the public not-found boundary");

    var emptyCareer = await service.GetPublicEligibleAsync(publicIdentity.NadiTrackId, Array.Empty<IdentityLinkedFinalsPlacementSelection>())
        ?? throw new InvalidOperationException("Expected empty public SP-4L career.");
    Assert(emptyCareer.History.Count == 0, "empty explicit scope selection returns an empty read model without guessing a ranking scope");

    var pointProperties = typeof(IdentityLinkedFinalsPlacementCareerPoint)
        .GetProperties()
        .Select(item => item.Name)
        .ToHashSet(StringComparer.Ordinal);
    foreach (var forbidden in new[]
    {
        "BirthDate", "Email", "Phone", "WssaId", "StackerId", "CompetitionId",
        "ParticipantCode", "ParticipantName", "ResultPublicId"
    })
    {
        Assert(!pointProperties.Contains(forbidden), $"SP-4L career point excludes {forbidden}");
    }

    var publicFinalsHistoryProperties = typeof(SportStackerFinalsHistoryPoint)
        .GetProperties()
        .Select(item => item.Name)
        .ToHashSet(StringComparer.Ordinal);
    Assert(!publicFinalsHistoryProperties.Contains("Placement") && !publicFinalsHistoryProperties.Contains("Rank"),
        "SP-4L does not silently publish placement through the existing public profile contract");

    // Multiple reviewed entry links for the same identity and competition are ambiguous for historical
    // participant-code association and must fail closed instead of choosing by name or row order.
    var duplicateStacker = NewStacker(c1.Id, "A1-DUP", "Career", "Stacker", "M", now);
    db.Stackers.Add(duplicateStacker);
    await db.SaveChangesAsync();
    db.StackerIdentityLinks.Add(NewLink(publicIdentity.Id, duplicateStacker.Id, "TEST_SP4L_AMBIGUOUS", now));
    await db.SaveChangesAsync();

    await AssertThrowsContainsAsync(
        async () =>
        {
            await service.GetPublicEligibleAsync(
                publicIdentity.NadiTrackId,
                new[]
                {
                    new IdentityLinkedFinalsPlacementSelection(
                        c1.Id,
                        new FinalsHistoricalPlacementScope("Individual", "Open", "Cycle", "normal", "all"))
                });
        },
        IdentityLinkedFinalsPlacementCareerBlockers.IdentityLinkAmbiguous,
        "ambiguous same-competition identity links fail closed");

    Console.WriteLine("SP-4L identity-linked Finals placement career read-model tests passed.");
}
catch (Exception ex)
{
    primaryFailure = ex;
    Console.Error.WriteLine($"SP-4L primary failure: {ex}");
}
finally
{
    try
    {
        var masterBuilder = new SqlConnectionStringBuilder(builder.ConnectionString) { InitialCatalog = "master" };
        await using var cleanup = new SqlConnection(masterBuilder.ConnectionString);
        using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await cleanup.OpenAsync(cleanupTimeout.Token);
        await using var command = cleanup.CreateCommand();
        command.CommandTimeout = 8;
        var escapedName = databaseName.Replace("]", "]]", StringComparison.Ordinal);
        command.CommandText = $"IF DB_ID(@databaseName) IS NOT NULL BEGIN ALTER DATABASE [{escapedName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{escapedName}]; END";
        command.Parameters.AddWithValue("databaseName", databaseName);
        await command.ExecuteNonQueryAsync(cleanupTimeout.Token);
        Console.WriteLine($"SP-4L cleanup: deleted {databaseName}");
    }
    catch (Exception ex)
    {
        cleanupFailure = ex;
        Console.Error.WriteLine($"SP-4L cleanup failed for {databaseName}: {ex.Message}");
    }
}

if (primaryFailure is not null)
{
    if (cleanupFailure is not null) Console.Error.WriteLine($"SP-4L cleanup secondary failure: {cleanupFailure.Message}");
    throw primaryFailure;
}
if (cleanupFailure is not null) throw new InvalidOperationException("SP-4L tests passed but cleanup failed.", cleanupFailure);

static IdentityLinkedFinalsPlacementCareerPoint Point(
    IdentityLinkedFinalsPlacementCareerReadModel career,
    string competitionKey,
    string eventCode,
    string category,
    string gender) =>
    career.History.Single(item =>
        item.CompetitionKey == competitionKey
        && item.Scope.EventCode == eventCode
        && item.Scope.Category == category
        && item.Scope.Gender == gender);

static SportStackerIdentity NewIdentity(
    string nadiTrackId,
    string firstName,
    string lastName,
    bool isPublic,
    DateTime now) => new()
{
    NadiTrackId = nadiTrackId,
    FirstName = firstName,
    LastName = lastName,
    Country = "MY",
    IsPublicProfile = isPublic,
    CreatedAt = now,
    UpdatedAt = now
};

static Competition NewCompetition(
    string key,
    string name,
    DateOnly date,
    string status,
    bool publiclyListed,
    long resultsRevision,
    DateTime now) => new()
{
    CompetitionCode = key,
    CompetitionKey = key,
    CompetitionName = name,
    Venue = "LocalDB",
    StartDate = date,
    EndDate = date,
    Status = status,
    IsPubliclyListed = publiclyListed,
    ResultsRevision = resultsRevision,
    CreatedAt = now,
    UpdatedAt = now
};

static Stacker NewStacker(
    int competitionId,
    string code,
    string firstName,
    string lastName,
    string gender,
    DateTime now) => new()
{
    CompetitionId = competitionId,
    StackerCode = code,
    FirstName = firstName,
    LastName = lastName,
    Gender = gender,
    Country = "MY",
    Paid = "No",
    CheckedIn = "No",
    CreatedAt = now,
    UpdatedAt = now
};

static StackerIdentityLink NewLink(long identityId, int stackerId, string reasonCode, DateTime now) => new()
{
    SportStackerIdentityId = identityId,
    StackerId = stackerId,
    MatchMethod = StackerIdentityMatchMethod.Manual,
    ResolutionReasonCode = reasonCode,
    ResolutionNote = "SP-4L reviewed historical identity link fixture.",
    LinkedAt = now
};

static CompetitionState NewState(string competitionKey, string json, long revision, DateTime now) => new()
{
    CompetitionKey = competitionKey,
    JsonData = json,
    SchemaVersion = "0.9-online",
    StateRevision = revision,
    CreatedAt = now,
    UpdatedAt = now,
    UpdatedBy = "sp4l-test"
};

static CompetitionResult NewResult(
    int competitionId,
    string participantCode,
    string eventCode,
    string attemptsJson,
    decimal penalty,
    long revision,
    DateTime now) => new()
{
    PublicId = Guid.NewGuid(),
    CompetitionId = competitionId,
    Stage = "Finals",
    ParticipantType = "Individual",
    ParticipantCode = participantCode,
    EventCode = eventCode,
    AttemptsJson = attemptsJson,
    Penalty = penalty,
    Revision = revision,
    CreatedAt = now,
    UpdatedAt = now
};

static void Assert(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException($"Assertion failed: {name}");
    Console.WriteLine($"PASS: {name}");
}

static async Task AssertThrowsContainsAsync(
    Func<Task> action,
    string expectedText,
    string name)
{
    try
    {
        await action();
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains(expectedText, StringComparison.Ordinal))
    {
        Console.WriteLine($"PASS: {name}");
        return;
    }

    throw new InvalidOperationException(
        $"Assertion failed: {name} (expected InvalidOperationException containing '{expectedText}').");
}
