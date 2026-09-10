using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Activities.SportStacking.Identity;
using StackMeet.Api.Data;
using StackMeet.Api.Models;

const string server = @"(localdb)\MSSQLLocalDB";
var databaseName = $"StackMeet_StackerIdentitySp2Test_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
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
    throw new InvalidOperationException("Refusing SP-2 integration test: unauthorized SQL server.");
if (!databaseName.StartsWith("StackMeet_StackerIdentitySp2Test_", StringComparison.Ordinal)
    || databaseName.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
    throw new InvalidOperationException("Refusing SP-2 cleanup: generated database name failed safety validation.");

Console.WriteLine($"SP-2 LocalDB harness: server={builder.DataSource}; database={builder.InitialCatalog}");
var options = new DbContextOptionsBuilder<StackMeetDbContext>().UseSqlServer(builder.ConnectionString).Options;
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
    var competition = NewCompetition("SP2A", "SP-2 Historical Backfill A", now);
    var otherCompetition = NewCompetition("SP2B", "SP-2 Historical Backfill B", now);
    db.Competitions.AddRange(competition, otherCompetition);
    await db.SaveChangesAsync();

    var existingIdentity = new SportStackerIdentity
    {
        NadiTrackId = "NDT-ABCDEFG",
        WssaId = "WSSA-100",
        FirstName = "Exact",
        LastName = "Athlete",
        Gender = "M",
        BirthDate = new DateOnly(2005, 5, 5),
        Country = "MY",
        Club = "Alpha Club",
        Region = "Selangor",
        Email = "existing@example.test",
        Phone = "60123456789",
        IsPublicProfile = false,
        CreatedAt = now,
        UpdatedAt = now
    };
    db.SportStackerIdentities.Add(existingIdentity);
    await db.SaveChangesAsync();

    var prelinked = NewStacker(competition.Id, "P0", "Already", "Linked", null, new DateOnly(2001, 1, 1), "MY", "Legacy Club", "prelinked@example.test", "60111111111");
    var strong = NewStacker(competition.Id, "S1", "Different", "Name", "WSSA-100", new DateOnly(1999, 1, 1), "SG", "Different Club", "strong@example.test", "60111111112");
    var possible = NewStacker(competition.Id, "S2", "Exact", "Athlete", null, new DateOnly(2006, 6, 6), "MY", "Alpha Club", "possible@example.test", "60111111113");
    var noCandidate = NewStacker(competition.Id, "S3", "Brand", "New", null, new DateOnly(2007, 7, 7), "MY", "Beta Club", "new@example.test", "60111111114");
    var explicitOnly = NewStacker(competition.Id, "S4", "Renamed", "Person", null, new DateOnly(2008, 8, 8), "SG", "Gamma Club", "renamed@example.test", "60111111115");
    var otherScope = NewStacker(otherCompetition.Id, "B1", "Different", "Name", "WSSA-100", new DateOnly(1998, 2, 2), "SG", "Other Club", "other@example.test", "60111111116");
    db.Stackers.AddRange(prelinked, strong, possible, noCandidate, explicitOnly, otherScope);
    await db.SaveChangesAsync();

    db.StackerIdentityLinks.Add(new StackerIdentityLink
    {
        SportStackerIdentityId = existingIdentity.Id,
        StackerId = prelinked.Id,
        MatchMethod = StackerIdentityMatchMethod.Manual,
        ResolutionReasonCode = "TEST_PRELINKED",
        ResolutionNote = "Seeded pre-existing historical link.",
        LinkedAt = now,
        LinkedByUserId = null
    });
    await db.SaveChangesAsync();

    var service = new StackerIdentityBackfillService(db, new SequenceIdGenerator("NDT-RSTUVWX"));
    var linksBeforeDiscovery = await db.StackerIdentityLinks.CountAsync();
    var report = await service.DiscoverAsync(competition.Id, 100);

    Assert(report.TotalStackers == 5, "competition-scoped historical inventory count");
    Assert(report.LinkedStackers == 1, "already-linked stackers excluded from backfill work");
    Assert(report.UnlinkedStackers == 4 && report.ReturnedItems == 4 && !report.HasMore, "all unlinked historical stackers discovered");
    Assert(report.ReviewRequiredItems == 2 && report.NoCandidateItems == 2, "discovery separates review-required and no-candidate entries");
    Assert(report.Items.All(item => item.CompetitionId == competition.Id), "competition filter prevents cross-competition leakage");
    Assert(await db.StackerIdentityLinks.CountAsync() == linksBeforeDiscovery, "discovery is read-only");

    var strongItem = report.Items.Single(item => item.StackerId == strong.Id);
    Assert(strongItem.Status == StackerIdentityBackfillItemStatus.ReviewRequired, "unique strong candidate still requires review");
    Assert(strongItem.Candidates.Count == 1
        && strongItem.Candidates[0].Strength == StackerIdentityMatchStrength.Strong
        && strongItem.Candidates[0].Evidence.Contains(StackerIdentityMatchMethod.WssaId), "WSSA evidence remains strong but non-authoritative");

    var possibleItem = report.Items.Single(item => item.StackerId == possible.Id);
    Assert(possibleItem.Status == StackerIdentityBackfillItemStatus.ReviewRequired
        && possibleItem.Candidates.Single().Strength == StackerIdentityMatchStrength.Possible, "name country club remains possible only");
    Assert(report.Items.Single(item => item.StackerId == noCandidate.Id).Status == StackerIdentityBackfillItemStatus.NoCandidates, "new historical person has no candidate");
    Assert(report.Items.Single(item => item.StackerId == explicitOnly.Id).Status == StackerIdentityBackfillItemStatus.NoCandidates, "changed historical snapshot is not guessed into an identity");

    var strongBlocked = await service.ApplyAsync(new StackerIdentityBackfillApplyRequest(
        strong.Id, null, StackerIdentityResolutionAction.LinkExisting, "NDT-ABCDEFG", false, false, null, 201));
    Assert(strongBlocked.Status == StackerIdentityBackfillApplyStatus.ResolutionBlocked
        && strongBlocked.Resolution?.Status == StackerIdentityResolutionStatus.ConfirmationRequired, "strong historical candidate cannot auto-link");
    Assert(!await db.StackerIdentityLinks.AnyAsync(link => link.StackerId == strong.Id), "blocked strong candidate writes nothing");

    var possibleBlocked = await service.ApplyAsync(new StackerIdentityBackfillApplyRequest(
        possible.Id, null, StackerIdentityResolutionAction.LinkExisting, "NDT-ABCDEFG", true, false, null, 202));
    Assert(possibleBlocked.Status == StackerIdentityBackfillApplyStatus.ResolutionBlocked
        && possibleBlocked.Resolution?.Status == StackerIdentityResolutionStatus.ResolutionNoteRequired, "possible historical candidate requires review note");
    Assert(!await db.StackerIdentityLinks.AnyAsync(link => link.StackerId == possible.Id), "possible match without note writes nothing");

    var possibleApplied = await service.ApplyAsync(new StackerIdentityBackfillApplyRequest(
        possible.Id, null, StackerIdentityResolutionAction.LinkExisting, "NDT-ABCDEFG", true, false, "Confirmed from archived registration records.", 203));
    Assert(possibleApplied.Status == StackerIdentityBackfillApplyStatus.Applied
        && possibleApplied.Persistence?.Link.MatchMethod == StackerIdentityMatchMethod.Manual, "reviewed possible candidate persists as manual link");
    Assert(possibleApplied.Persistence?.Link.ResolutionNote == "Confirmed from archived registration records.", "historical review note is persisted");

    var strongApplied = await service.ApplyAsync(new StackerIdentityBackfillApplyRequest(
        strong.Id, null, StackerIdentityResolutionAction.LinkExisting, "NDT-ABCDEFG", true, false, null, 204));
    Assert(strongApplied.Status == StackerIdentityBackfillApplyStatus.Applied
        && strongApplied.Persistence?.Link.MatchMethod == StackerIdentityMatchMethod.WssaId, "confirmed strong WSSA candidate links with provenance");

    var exactApplied = await service.ApplyAsync(new StackerIdentityBackfillApplyRequest(
        explicitOnly.Id, "NDT-ABCDEFG", StackerIdentityResolutionAction.LinkExisting, null, false, false, null, 205));
    Assert(exactApplied.Status == StackerIdentityBackfillApplyStatus.Applied
        && exactApplied.Resolution?.ReasonCode == "APPROVED_EXACT_NADITRACK_LINK"
        && exactApplied.Persistence?.Link.MatchMethod == StackerIdentityMatchMethod.NadiTrackId, "explicit NADITrack ID is authoritative for historical linking");

    var newApplied = await service.ApplyAsync(new StackerIdentityBackfillApplyRequest(
        noCandidate.Id, null, StackerIdentityResolutionAction.CreateNew, null, false, false, null, 206));
    Assert(newApplied.Status == StackerIdentityBackfillApplyStatus.Applied
        && newApplied.Persistence?.IdentityCreated == true
        && newApplied.NadiTrackId == "NDT-RSTUVWX", "no-candidate historical entry can deliberately create a permanent identity");
    Assert(newApplied.Persistence?.Link.MatchMethod == StackerIdentityMatchMethod.CreatedNew, "historical create-new retains CREATED_NEW provenance");

    var alreadyLinked = await service.ApplyAsync(new StackerIdentityBackfillApplyRequest(
        prelinked.Id, null, StackerIdentityResolutionAction.LinkExisting, "NDT-ABCDEFG", true, false, null, 207));
    Assert(alreadyLinked.Status == StackerIdentityBackfillApplyStatus.AlreadyLinked
        && alreadyLinked.NadiTrackId == "NDT-ABCDEFG", "already-linked historical stacker is idempotently surfaced without rewrite");

    var unknownExplicit = await service.ApplyAsync(new StackerIdentityBackfillApplyRequest(
        otherScope.Id, "NDT-2345678", StackerIdentityResolutionAction.CreateNew, null, false, false, null, 208));
    Assert(unknownExplicit.Status == StackerIdentityBackfillApplyStatus.ResolutionBlocked
        && unknownExplicit.Resolution?.Status == StackerIdentityResolutionStatus.LookupBlocked, "unknown explicit NADITrack ID cannot fall through to historical create-new");
    Assert(!await db.StackerIdentityLinks.AnyAsync(link => link.StackerId == otherScope.Id), "unknown explicit ID writes nothing");

    var finalReport = await service.DiscoverAsync(competition.Id, 100);
    Assert(finalReport.LinkedStackers == 5 && finalReport.UnlinkedStackers == 0 && finalReport.ReturnedItems == 0, "completed competition backfill becomes empty and idempotent");

    var globalReport = await service.DiscoverAsync(null, 100);
    Assert(globalReport.TotalStackers == 6 && globalReport.LinkedStackers == 5 && globalReport.UnlinkedStackers == 1, "global inventory retains unresolved stackers from other competitions");

    await AssertThrowsAsync<ArgumentOutOfRangeException>(
        async () => { await service.DiscoverAsync(competition.Id, StackerIdentityBackfillService.MaximumDiscoveryItems + 1); },
        "discovery batch size is bounded");

    Console.WriteLine("SP-2 historical stacker identity backfill integration tests passed.");
}
catch (Exception ex)
{
    primaryFailure = ex;
    Console.Error.WriteLine($"SP-2 primary failure: {ex}");
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
        var escapedName = databaseName.Replace("]", "]]");
        command.CommandText = $"IF DB_ID(@databaseName) IS NOT NULL BEGIN ALTER DATABASE [{escapedName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{escapedName}]; END";
        command.Parameters.AddWithValue("@databaseName", databaseName);
        await command.ExecuteNonQueryAsync(cleanupTimeout.Token);
        Console.WriteLine($"SP-2 cleanup: deleted {databaseName}");
    }
    catch (Exception ex)
    {
        cleanupFailure = ex;
        Console.Error.WriteLine($"SP-2 cleanup failed for {databaseName}: {ex.Message}");
    }
}

if (primaryFailure is not null)
{
    if (cleanupFailure is not null) Console.Error.WriteLine($"SP-2 cleanup secondary failure: {cleanupFailure.Message}");
    throw primaryFailure;
}
if (cleanupFailure is not null) throw new InvalidOperationException("SP-2 tests passed but cleanup failed.", cleanupFailure);

static Competition NewCompetition(string key, string name, DateTime now) => new()
{
    CompetitionCode = key,
    CompetitionKey = key,
    CompetitionName = name,
    Venue = "LocalDB",
    StartDate = DateOnly.FromDateTime(now),
    EndDate = DateOnly.FromDateTime(now),
    Status = "Active",
    CreatedAt = now,
    UpdatedAt = now
};

static Stacker NewStacker(
    int competitionId,
    string code,
    string firstName,
    string lastName,
    string? wssaId,
    DateOnly? birthDate,
    string country,
    string? club,
    string? email,
    string? phone) => new()
{
    CompetitionId = competitionId,
    StackerCode = code,
    WssaId = wssaId,
    FirstName = firstName,
    LastName = lastName,
    Gender = "M",
    BirthDate = birthDate,
    Country = country,
    Club = club,
    Email = email,
    Phone = phone,
    Paid = "No",
    CheckedIn = "No",
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};

static void Assert(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException($"Failed scenario: {name}");
    Console.WriteLine($"PASS {name}");
}

static async Task AssertThrowsAsync<TException>(Func<Task> action, string name)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        Console.WriteLine($"PASS {name}");
        return;
    }

    throw new InvalidOperationException($"Failed scenario: {name}; expected {typeof(TException).Name}.");
}

sealed class SequenceIdGenerator(params string[] values) : INadiTrackIdGenerator
{
    private readonly Queue<string> queue = new(values);

    public string Generate()
    {
        if (queue.Count == 0) throw new InvalidOperationException("Test NADITrack ID generator exhausted.");
        return queue.Dequeue();
    }
}
