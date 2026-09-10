using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Activities.SportStacking.Identity;
using StackMeet.Api.Data;
using StackMeet.Api.Models;

const string server = @"(localdb)\MSSQLLocalDB";
var databaseName = $"StackMeet_StackerIdentitySp1Test_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
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
    throw new InvalidOperationException("Refusing SP-1 integration test: unauthorized SQL server.");
if (!databaseName.StartsWith("StackMeet_StackerIdentitySp1Test_", StringComparison.Ordinal)
    || databaseName.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
    throw new InvalidOperationException("Refusing SP-1 cleanup: generated database name failed safety validation.");

Console.WriteLine($"SP-1 LocalDB harness: server={builder.DataSource}; database={builder.InitialCatalog}");
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

    Assert(await TableExistsAsync(db, "dbo", "SportStackerIdentity"), "identity table migrated");
    Assert(await TableExistsAsync(db, "dbo", "StackerIdentityLink"), "identity link table migrated");

    var identityEntity = db.Model.FindEntityType(typeof(SportStackerIdentity))
        ?? throw new InvalidOperationException("SportStackerIdentity is missing from the EF model.");
    var linkEntity = db.Model.FindEntityType(typeof(StackerIdentityLink))
        ?? throw new InvalidOperationException("StackerIdentityLink is missing from the EF model.");
    Assert(identityEntity.GetIndexes().Any(index => index.IsUnique && index.Properties.Select(p => p.Name).SequenceEqual([nameof(SportStackerIdentity.NadiTrackId)])), "NADITrack ID unique model index");
    Assert(linkEntity.GetIndexes().Any(index => index.IsUnique && index.Properties.Select(p => p.Name).SequenceEqual([nameof(StackerIdentityLink.StackerId)])), "one permanent identity link per competition stacker");

    var now = DateTime.UtcNow;
    var competition = new Competition
    {
        CompetitionCode = "SP1TEST",
        CompetitionKey = "SP1TEST",
        CompetitionName = "SP-1 Identity Persistence Test",
        Venue = "LocalDB",
        StartDate = DateOnly.FromDateTime(now),
        EndDate = DateOnly.FromDateTime(now),
        Status = "Active",
        CreatedAt = now,
        UpdatedAt = now
    };
    db.Competitions.Add(competition);
    await db.SaveChangesAsync();

    var exactStacker = NewStacker(competition.Id, "S1", "Exact", "Athlete", "Alpha Club");
    var newStacker = NewStacker(competition.Id, "S2", "Brand", "New", "Beta Club");
    var manualStacker = NewStacker(competition.Id, "S3", "Exact", "Athlete", "Alpha Club");
    manualStacker.BirthDate = new DateOnly(2006, 6, 6);
    var overrideStacker = NewStacker(competition.Id, "S4", "Exact", "Athlete", "Alpha Club");
    db.Stackers.AddRange(exactStacker, newStacker, manualStacker, overrideStacker);

    var existingIdentity = new SportStackerIdentity
    {
        NadiTrackId = "NDT-ABCDEFG",
        WssaId = "WSSA-EXISTING",
        FirstName = "Exact",
        LastName = "Athlete",
        Gender = "M",
        BirthDate = new DateOnly(2005, 5, 5),
        Country = "MY",
        Club = "Alpha Club",
        IsPublicProfile = false,
        CreatedAt = now,
        UpdatedAt = now
    };
    db.Set<SportStackerIdentity>().Add(existingIdentity);
    await db.SaveChangesAsync();

    var identities = await db.Set<SportStackerIdentity>().AsNoTracking().ToListAsync();

    var exactMatch = StackerIdentityMatcher.FindMatches(
        new StackerIdentityMatchQuery("NDT-ABCDEFG", null, null, null, null, null, null, null, null),
        identities);
    var exactDecision = StackerIdentityResolutionPolicy.Resolve(
        exactMatch,
        new StackerIdentityResolutionRequest(StackerIdentityResolutionAction.LinkExisting, "NDT-ABCDEFG", false, false, null));
    var exactService = new StackerIdentityPersistenceService(db, new SequenceIdGenerator("NDT-HJKMNPQ"));
    var exactResult = await exactService.PersistAsync(new StackerIdentityPersistenceRequest(exactStacker.Id, exactDecision, null, 101));
    Assert(!exactResult.IdentityCreated, "exact identity link does not create person");
    Assert(exactResult.Identity.Id == existingIdentity.Id, "exact identity link resolves persisted person");
    Assert(exactResult.Link.MatchMethod == StackerIdentityMatchMethod.NadiTrackId, "exact link provenance persisted");
    Assert(exactResult.Link.LinkedByUserId == 101, "operator id persisted");

    identities = await db.Set<SportStackerIdentity>().AsNoTracking().ToListAsync();
    var newMatch = StackerIdentityMatcher.FindMatches(QueryFrom(newStacker), identities);
    Assert(newMatch.Candidates.Count == 0, "new stacker has no duplicate candidates");
    var newDecision = StackerIdentityResolutionPolicy.Resolve(
        newMatch,
        new StackerIdentityResolutionRequest(StackerIdentityResolutionAction.CreateNew, null, false, false, null));
    var newService = new StackerIdentityPersistenceService(
        db,
        new SequenceIdGenerator("NDT-ABCDEFG", "NDT-HJKMNPQ"));
    var newResult = await newService.PersistAsync(new StackerIdentityPersistenceRequest(newStacker.Id, newDecision, null, 102));
    Assert(newResult.IdentityCreated, "CreateNew creates permanent identity");
    Assert(newResult.Identity.NadiTrackId == "NDT-HJKMNPQ", "issuer retries an existing generated ID");
    Assert(newResult.Identity.FirstName == newStacker.FirstName && newResult.Identity.LastName == newStacker.LastName, "new identity seeded from competition snapshot");
    Assert(!newResult.Identity.IsPublicProfile, "new permanent profile is private by default");
    Assert(newResult.Link.MatchMethod == StackerIdentityMatchMethod.CreatedNew, "new identity provenance persisted");

    identities = await db.Set<SportStackerIdentity>().AsNoTracking().ToListAsync();
    var manualMatch = StackerIdentityMatcher.FindMatches(QueryFrom(manualStacker), identities);
    var manualDecision = StackerIdentityResolutionPolicy.Resolve(
        manualMatch,
        new StackerIdentityResolutionRequest(StackerIdentityResolutionAction.LinkExisting, "NDT-ABCDEFG", true, false, "Confirmed against school registration record."));
    Assert(manualDecision.CanProceedToPersistence && manualDecision.LinkMatchMethod == StackerIdentityMatchMethod.Manual, "possible candidate becomes reviewed manual link");
    var manualService = new StackerIdentityPersistenceService(db, new SequenceIdGenerator("NDT-RSTUVWX"));
    var manualResult = await manualService.PersistAsync(new StackerIdentityPersistenceRequest(manualStacker.Id, manualDecision, "Confirmed against school registration record.", 103));
    Assert(manualResult.Link.ResolutionNote == "Confirmed against school registration record.", "manual review note persisted");

    identities = await db.Set<SportStackerIdentity>().AsNoTracking().ToListAsync();
    var overrideMatch = StackerIdentityMatcher.FindMatches(QueryFrom(overrideStacker), identities);
    Assert(overrideMatch.Candidates.Count > 0, "duplicate candidates detected before override create");
    var overrideDecision = StackerIdentityResolutionPolicy.Resolve(
        overrideMatch,
        new StackerIdentityResolutionRequest(StackerIdentityResolutionAction.CreateNew, null, false, true, "Different person confirmed by organizer."));
    var overrideService = new StackerIdentityPersistenceService(db, new SequenceIdGenerator("NDT-RSTUVWX"));
    var overrideResult = await overrideService.PersistAsync(new StackerIdentityPersistenceRequest(overrideStacker.Id, overrideDecision, "Different person confirmed by organizer.", 104));
    Assert(overrideResult.IdentityCreated, "approved duplicate override can create distinct identity");
    Assert(overrideResult.Link.ResolutionReasonCode == "APPROVED_CREATE_NEW_DESPITE_CANDIDATES", "duplicate override reason persisted");
    Assert(overrideResult.Link.ResolutionNote == "Different person confirmed by organizer.", "duplicate override note persisted");

    var identityCountBeforeDuplicateLink = await db.Set<SportStackerIdentity>().CountAsync();
    await AssertThrowsAsync<InvalidOperationException>(
        () => newService.PersistAsync(new StackerIdentityPersistenceRequest(newStacker.Id, newDecision, null, 105)),
        "same competition stacker cannot be linked twice");
    Assert(await db.Set<SportStackerIdentity>().CountAsync() == identityCountBeforeDuplicateLink, "duplicate-link rejection creates no orphan identity");

    var blockedDecision = new StackerIdentityResolutionDecision(
        StackerIdentityResolutionStatus.ConfirmationRequired,
        StackerIdentityResolutionAction.None,
        existingIdentity,
        null,
        "CANDIDATE_CONFIRMATION_REQUIRED");
    await AssertThrowsAsync<InvalidOperationException>(
        () => new StackerIdentityPersistenceService(db, new SequenceIdGenerator("NDT-YZ23456"))
            .PersistAsync(new StackerIdentityPersistenceRequest(exactStacker.Id, blockedDecision, null, 106)),
        "unapproved SP-0C decision cannot persist");

    await using (var uniqueDb = new StackMeetDbContext(options))
    {
        uniqueDb.Set<SportStackerIdentity>().Add(new SportStackerIdentity
        {
            NadiTrackId = "NDT-ABCDEFG",
            FirstName = "Duplicate",
            LastName = "Identifier",
            Gender = "F",
            Country = "MY",
            IsPublicProfile = false,
            CreatedAt = now,
            UpdatedAt = now
        });
        await AssertThrowsAsync<DbUpdateException>(() => uniqueDb.SaveChangesAsync(), "database enforces unique NADITrack ID");
    }

    Console.WriteLine("SP-1 stacker identity persistence integration tests passed.");
}
catch (Exception ex)
{
    primaryFailure = ex;
    Console.Error.WriteLine($"SP-1 primary failure: {ex}");
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
        Console.WriteLine($"SP-1 cleanup: deleted {databaseName}");
    }
    catch (Exception ex)
    {
        cleanupFailure = ex;
        Console.Error.WriteLine($"SP-1 cleanup failed for {databaseName}: {ex.Message}");
    }
}

if (primaryFailure is not null)
{
    if (cleanupFailure is not null) Console.Error.WriteLine($"SP-1 cleanup secondary failure: {cleanupFailure.Message}");
    throw primaryFailure;
}
if (cleanupFailure is not null) throw new InvalidOperationException("SP-1 tests passed but cleanup failed.", cleanupFailure);

static Stacker NewStacker(int competitionId, string code, string firstName, string lastName, string club) => new()
{
    CompetitionId = competitionId,
    StackerCode = code,
    WssaId = null,
    FirstName = firstName,
    LastName = lastName,
    Gender = "M",
    BirthDate = new DateOnly(2005, 5, 5),
    Country = "MY",
    Club = club,
    Email = $"{code.ToLowerInvariant()}@example.test",
    Phone = $"6012000{code[1]}000",
    Paid = "No",
    CheckedIn = "No",
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};

static StackerIdentityMatchQuery QueryFrom(Stacker stacker) => new(
    null,
    stacker.WssaId,
    stacker.FirstName,
    stacker.LastName,
    stacker.BirthDate,
    stacker.Country,
    stacker.Club,
    stacker.Email,
    stacker.Phone);

static async Task<bool> TableExistsAsync(StackMeetDbContext db, string schema, string table)
{
    var connection = db.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT CASE WHEN OBJECT_ID(@qualifiedName, 'U') IS NULL THEN 0 ELSE 1 END";
    var parameter = command.CreateParameter();
    parameter.ParameterName = "@qualifiedName";
    parameter.Value = $"{schema}.{table}";
    command.Parameters.Add(parameter);
    return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
}

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
        if (queue.Count == 0) throw new InvalidOperationException("Test ID generator exhausted.");
        return queue.Dequeue();
    }
}
