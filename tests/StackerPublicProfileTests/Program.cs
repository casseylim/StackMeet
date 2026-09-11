using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Activities.SportStacking.Identity;
using StackMeet.Api.Controllers;
using StackMeet.Api.Data;
using StackMeet.Api.Models;

const string server = @"(localdb)\MSSQLLocalDB";
var databaseName = $"StackMeet_StackerPublicProfileSp3bTest_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
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
    throw new InvalidOperationException("Refusing SP-3B integration test: unauthorized SQL server.");
if (!databaseName.StartsWith("StackMeet_StackerPublicProfileSp3bTest_", StringComparison.Ordinal)
    || databaseName.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
    throw new InvalidOperationException("Refusing SP-3B cleanup: generated database name failed safety validation.");

Console.WriteLine($"SP-3B LocalDB harness: server={builder.DataSource}; database={builder.InitialCatalog}");
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
    var publicIdentity = new SportStackerIdentity
    {
        NadiTrackId = "NDT-ABCDEFG",
        FirstName = "Ava",
        LastName = "Stacker",
        Country = "MY",
        Club = "NADI Club",
        Region = "Selangor",
        IsPublicProfile = true,
        CreatedAt = now,
        UpdatedAt = now
    };
    var privateIdentity = new SportStackerIdentity
    {
        NadiTrackId = "NDT-HJKMNPQ",
        FirstName = "Private",
        LastName = "Athlete",
        Country = "MY",
        IsPublicProfile = false,
        CreatedAt = now,
        UpdatedAt = now
    };
    db.SportStackerIdentities.AddRange(publicIdentity, privateIdentity);

    var competition = new Competition
    {
        CompetitionCode = "SP3B-CLOSED",
        CompetitionKey = "SP3B-CLOSED",
        CompetitionName = "SP-3B Closed Championship",
        Venue = "LocalDB",
        StartDate = new DateOnly(2026, 8, 16),
        EndDate = new DateOnly(2026, 8, 16),
        Status = "Closed",
        IsPubliclyListed = true,
        CreatedAt = now,
        UpdatedAt = now
    };
    db.Competitions.Add(competition);
    await db.SaveChangesAsync();

    var publicStacker = NewStacker(competition.Id, "PUB-1", "Ava", "Stacker", now);
    var privateStacker = NewStacker(competition.Id, "PVT-1", "Private", "Athlete", now);
    db.Stackers.AddRange(publicStacker, privateStacker);
    await db.SaveChangesAsync();

    db.StackerIdentityLinks.AddRange(
        Link(publicIdentity.Id, publicStacker.Id, now),
        Link(privateIdentity.Id, privateStacker.Id, now));
    db.CompetitionResults.Add(new CompetitionResult
    {
        CompetitionId = competition.Id,
        Stage = "Finals",
        ParticipantType = "Individual",
        ParticipantCode = publicStacker.StackerCode,
        EventCode = "3-3-3",
        AttemptsJson = JsonSerializer.Serialize(new[] { 4.321m, 4.500m, 999m }),
        Penalty = 0m,
        Revision = 1,
        CreatedAt = now,
        UpdatedAt = now
    });
    await db.SaveChangesAsync();

    var service = new SportStackerCareerProfileService(db);
    var controller = new PublicStackerProfilesController(service);

    var publicAction = await controller.Get("ndt-abcdefg", CancellationToken.None);
    var publicOk = publicAction.Result as OkObjectResult
        ?? throw new InvalidOperationException("Expected public SP-3B profile to return HTTP 200.");
    var profile = publicOk.Value as PublicSportStackerCareerProfile
        ?? throw new InvalidOperationException("Expected SP-3B endpoint to return the public career contract.");

    Assert(profile.NadiTrackId == "NDT-ABCDEFG", "public endpoint normalizes permanent NADITrack ID");
    Assert(profile.DisplayName == "Ava Stacker", "public endpoint returns reviewed display name");
    Assert(profile.CompetitionCount == 1, "public endpoint returns finalized public career count");
    Assert(profile.PersonalBests.Count == 1 && profile.PersonalBests[0].OfficialTime == 4.321m,
        "public endpoint returns finalized personal best projection");

    Assert((await controller.Get("NDT-HJKMNPQ", CancellationToken.None)).Result is NotFoundResult,
        "private profile returns the same not-found boundary");
    Assert((await controller.Get("NDT-RSTUVWX", CancellationToken.None)).Result is NotFoundResult,
        "unknown valid profile returns not found");
    Assert((await controller.Get("not-an-id", CancellationToken.None)).Result is NotFoundResult,
        "malformed profile ID returns not found");

    var apiRoute = typeof(PublicStackerProfilesController).GetCustomAttribute<RouteAttribute>();
    Assert(apiRoute?.Template == "api/public/stackers/{nadiTrackId}",
        "public profile API stays under the existing unauthenticated public boundary");
    var cache = typeof(PublicStackerProfilesController).GetCustomAttribute<ResponseCacheAttribute>();
    Assert(cache is { NoStore: true, Location: ResponseCacheLocation.None },
        "public profile API is explicitly non-cacheable");

    var pageRoute = typeof(StackerProfilePageController).GetCustomAttribute<RouteAttribute>();
    Assert(pageRoute?.Template == "Stackers/{nadiTrackId}",
        "permanent public profile page route is stable");

    Console.WriteLine("SP-3B public profile endpoint integration tests passed.");
}
catch (Exception ex)
{
    primaryFailure = ex;
    Console.Error.WriteLine($"SP-3B primary failure: {ex}");
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
        Console.WriteLine($"SP-3B cleanup: deleted {databaseName}");
    }
    catch (Exception ex)
    {
        cleanupFailure = ex;
        Console.Error.WriteLine($"SP-3B cleanup failed for {databaseName}: {ex.Message}");
    }
}

if (primaryFailure is not null)
{
    if (cleanupFailure is not null) Console.Error.WriteLine($"SP-3B cleanup secondary failure: {cleanupFailure.Message}");
    throw primaryFailure;
}
if (cleanupFailure is not null) throw new InvalidOperationException("SP-3B tests passed but cleanup failed.", cleanupFailure);

static Stacker NewStacker(int competitionId, string code, string firstName, string lastName, DateTime now) => new()
{
    CompetitionId = competitionId,
    StackerCode = code,
    FirstName = firstName,
    LastName = lastName,
    Gender = "F",
    Country = "MY",
    Paid = "No",
    CheckedIn = "No",
    CreatedAt = now,
    UpdatedAt = now
};

static StackerIdentityLink Link(long identityId, int stackerId, DateTime now) => new()
{
    SportStackerIdentityId = identityId,
    StackerId = stackerId,
    MatchMethod = StackerIdentityMatchMethod.Manual,
    ResolutionReasonCode = "TEST_SP3B_LINK",
    ResolutionNote = "SP-3B public profile fixture.",
    LinkedAt = now
};

static void Assert(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException($"Failed scenario: {name}");
    Console.WriteLine($"PASS {name}");
}
