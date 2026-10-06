using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StackMeet.Api.Activities.SportStacking.Identity;
using StackMeet.Api.Data;
using StackMeet.Api.Models;

const string executeFlag = "--execute";
const string publicFlag = "--public";

var arguments = ParseArguments(args);
var competitionId = RequiredInt(arguments, "--competition-id");
var stackerCode = Required(arguments, "--stacker-code").Trim();
var expectedDisplayName = Required(arguments, "--expected-display-name").Trim();
var execute = arguments.ContainsKey(executeFlag);
var publish = arguments.ContainsKey(publicFlag);
var operatorNote = Optional(arguments, "--operator-note");
var selectedNadiTrackId = Optional(arguments, "--existing-naditrack-id");

if (competitionId <= 0) Fail("--competition-id must be a positive integer.");
if (string.IsNullOrWhiteSpace(stackerCode)) Fail("--stacker-code is required.");
if (string.IsNullOrWhiteSpace(expectedDisplayName)) Fail("--expected-display-name is required.");
if (execute && !publish) Fail("Execution requires --public; this tool is only for governed public-profile activation.");
if (execute && string.IsNullOrWhiteSpace(operatorNote)) Fail("Execution requires --operator-note for the audit trail.");

var config = new ConfigurationBuilder()
    .AddUserSecrets<StackMeetDbContext>(optional: true)
    .AddEnvironmentVariables()
    .Build();

var connectionString =
    config.GetConnectionString("StackMeet")
    ?? config["STACKMEET_CONNECTION_STRING"];

if (string.IsNullOrWhiteSpace(connectionString))
{
    Fail("Production connection is unavailable. Configure ConnectionStrings:StackMeet in user-secrets or STACKMEET_CONNECTION_STRING. The connection value is never printed.");
}

var options = new DbContextOptionsBuilder<StackMeetDbContext>()
    .UseSqlServer(connectionString)
    .Options;

await using var database = new StackMeetDbContext(options);

var competition = await database.Competitions.AsNoTracking()
    .SingleOrDefaultAsync(item => item.Id == competitionId)
    ?? throw new InvalidOperationException($"Competition {competitionId} was not found.");

var stacker = await database.Stackers.AsNoTracking()
    .SingleOrDefaultAsync(item => item.CompetitionId == competitionId && item.StackerCode == stackerCode)
    ?? throw new InvalidOperationException($"Stacker '{stackerCode}' was not found in competition {competitionId}.");

var displayName = $"{stacker.FirstName} {stacker.LastName}".Trim();
if (!string.Equals(displayName, expectedDisplayName, StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        $"Expected display name mismatch. Requested '{expectedDisplayName}', live row is '{displayName}'. No write attempted.");
}

if (string.IsNullOrWhiteSpace(stacker.FirstName)
    || string.IsNullOrWhiteSpace(stacker.LastName)
    || string.IsNullOrWhiteSpace(stacker.Gender)
    || string.IsNullOrWhiteSpace(stacker.Country))
{
    throw new InvalidOperationException(
        "Stacker snapshot is incomplete for permanent identity creation. No write attempted.");
}

Console.WriteLine("PRODUCTION_IDENTITY_ACTIVATION_TOOL=READY");
Console.WriteLine($"COMPETITION_ID={competition.Id}");
Console.WriteLine($"COMPETITION_CODE={competition.CompetitionCode}");
Console.WriteLine($"STACKER_CODE={stacker.StackerCode}");
Console.WriteLine($"DISPLAY_NAME={displayName}");
Console.WriteLine($"MODE={(execute ? "EXECUTE" : "DRY-RUN")}");

var existingLink = await database.StackerIdentityLinks.AsNoTracking()
    .Include(item => item.SportStackerIdentity)
    .SingleOrDefaultAsync(item => item.StackerId == stacker.Id);

if (existingLink is not null)
{
    var linkedIdentity = existingLink.SportStackerIdentity;
    Console.WriteLine("IDENTITY_LINK_STATE=EXISTING");
    Console.WriteLine($"NADITRACK_ID={linkedIdentity.NadiTrackId}");
    Console.WriteLine($"PUBLIC_PROFILE_CURRENT={linkedIdentity.IsPublicProfile}");

    if (!execute)
    {
        Console.WriteLine("DRY_RUN=PASS");
        Console.WriteLine("PRODUCTION_WRITES=0");
        return;
    }

    RequireConfirmation(arguments, competitionId, stackerCode);

    if (!linkedIdentity.IsPublicProfile)
    {
        var writable = await database.SportStackerIdentities
            .SingleAsync(item => item.Id == linkedIdentity.Id);
        writable.IsPublicProfile = true;
        writable.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync();
    }

    await VerifyPublicProfileAsync(database, linkedIdentity.NadiTrackId);
    Console.WriteLine("IDENTITY_CREATED=FALSE");
    Console.WriteLine("IDENTITY_LINK_CREATED=FALSE");
    Console.WriteLine("PUBLIC_PROFILE_ACTIVATED=TRUE");
    Console.WriteLine("PRODUCTION_WRITE=PASS");
    return;
}

var identities = await database.SportStackerIdentities.AsNoTracking().ToListAsync();
var match = StackerIdentityMatcher.FindMatches(QueryFrom(stacker), identities);

Console.WriteLine($"IDENTITY_LOOKUP_STATUS={match.Status}");
Console.WriteLine($"IDENTITY_CANDIDATE_COUNT={match.Candidates.Count}");

StackerIdentityResolutionDecision resolution;
string? resolutionNote = operatorNote;

if (!string.IsNullOrWhiteSpace(selectedNadiTrackId))
{
    var normalized = NadiTrackIdRules.Normalize(selectedNadiTrackId);
    var selected = identities.SingleOrDefault(item =>
        string.Equals(item.NadiTrackId, normalized, StringComparison.Ordinal));

    if (selected is null)
    {
        throw new InvalidOperationException(
            $"Requested existing NADITrack ID '{normalized}' was not found. No write attempted.");
    }

    var reviewedMatch = StackerIdentityMatcher.FindMatches(
        new StackerIdentityMatchQuery(
            normalized,
            stacker.WssaId,
            stacker.FirstName,
            stacker.LastName,
            stacker.BirthDate,
            stacker.Country,
            stacker.Club,
            stacker.Email,
            stacker.Phone),
        identities);

    resolution = StackerIdentityResolutionPolicy.Resolve(
        reviewedMatch,
        new StackerIdentityResolutionRequest(
            StackerIdentityResolutionAction.LinkExisting,
            normalized,
            true,
            false,
            resolutionNote));
}
else if (match.Candidates.Count == 0)
{
    resolution = StackerIdentityResolutionPolicy.Resolve(
        match,
        new StackerIdentityResolutionRequest(
            StackerIdentityResolutionAction.CreateNew,
            null,
            false,
            false,
            resolutionNote));
}
else
{
    Console.WriteLine("ACTION_REQUIRED=REVIEW_EXISTING_IDENTITY_CANDIDATES");
    foreach (var candidate in match.Candidates.Take(10))
    {
        Console.WriteLine(
            $"CANDIDATE={candidate.Identity.NadiTrackId}|STRENGTH={candidate.Strength}|EVIDENCE={string.Join(",", candidate.Evidence)}");
    }

    throw new InvalidOperationException(
        "Existing identity candidates require explicit operator review and --existing-naditrack-id. No write attempted.");
}

Console.WriteLine($"RESOLUTION_STATUS={resolution.Status}");
Console.WriteLine($"RESOLUTION_ACTION={resolution.Action}");
Console.WriteLine($"RESOLUTION_REASON={resolution.ReasonCode}");

if (!resolution.CanProceedToPersistence)
{
    throw new InvalidOperationException(
        $"Identity resolution is not approved ({resolution.Status}). No write attempted.");
}

if (!execute)
{
    Console.WriteLine("DRY_RUN=PASS");
    Console.WriteLine("PRODUCTION_WRITES=0");
    return;
}

RequireConfirmation(arguments, competitionId, stackerCode);

var persistence = new StackerIdentityPersistenceService(
    database,
    new CryptographicNadiTrackIdGenerator());

var persisted = await persistence.PersistAsync(
    new StackerIdentityPersistenceRequest(
        stacker.Id,
        resolution,
        resolutionNote,
        LinkedByUserId: null));

Console.WriteLine($"NADITRACK_ID={persisted.Identity.NadiTrackId}");
Console.WriteLine($"IDENTITY_CREATED={persisted.IdentityCreated}");
Console.WriteLine("IDENTITY_LINK_CREATED=TRUE");
Console.WriteLine($"PUBLIC_PROFILE_CURRENT={persisted.Identity.IsPublicProfile}");

if (!persisted.Identity.IsPublicProfile)
{
    var writable = await database.SportStackerIdentities
        .SingleAsync(item => item.Id == persisted.Identity.Id);
    writable.IsPublicProfile = true;
    writable.UpdatedAt = DateTime.UtcNow;
    await database.SaveChangesAsync();
}

await VerifyPublicProfileAsync(database, persisted.Identity.NadiTrackId);
Console.WriteLine("PUBLIC_PROFILE_ACTIVATED=TRUE");
Console.WriteLine("PRODUCTION_WRITE=PASS");

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

static async Task VerifyPublicProfileAsync(StackMeetDbContext database, string nadiTrackId)
{
    var profile = await new SportStackerCareerProfileService(database)
        .GetPublicAsync(nadiTrackId);

    if (profile is null)
    {
        throw new InvalidOperationException(
            "Identity/link write completed but public career projection did not resolve. Stop and investigate before any further activation.");
    }

    Console.WriteLine("PUBLIC_PROFILE_PROJECTION=PASS");
}

static void RequireConfirmation(
    IReadOnlyDictionary<string, string?> arguments,
    int competitionId,
    string stackerCode)
{
    var expected = $"ACTIVATE PUBLIC PROFILE {competitionId}/{stackerCode}";
    var actual = Optional(arguments, "--confirmation");
    if (!string.Equals(actual, expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Execution confirmation failed. Expected exact phrase: {expected}");
    }
}

static Dictionary<string, string?> ParseArguments(string[] source)
{
    var parsed = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    for (var index = 0; index < source.Length; index++)
    {
        var current = source[index];
        if (!current.StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Unexpected argument '{current}'.");
        }

        if (current is executeFlag or publicFlag)
        {
            parsed[current] = null;
            continue;
        }

        if (index + 1 >= source.Length || source[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Argument '{current}' requires a value.");
        }

        parsed[current] = source[++index];
    }

    return parsed;
}

static string Required(IReadOnlyDictionary<string, string?> parsed, string name) =>
    parsed.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new ArgumentException($"Missing required argument {name}.");

static int RequiredInt(IReadOnlyDictionary<string, string?> parsed, string name) =>
    int.TryParse(Required(parsed, name), out var value)
        ? value
        : throw new ArgumentException($"{name} must be an integer.");

static string? Optional(IReadOnlyDictionary<string, string?> parsed, string name) =>
    parsed.TryGetValue(name, out var value) ? value : null;

static void Fail(string message) => throw new InvalidOperationException(message);
