using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;

namespace StackMeet.Api.Controllers;

[ApiController]
[Route("api/public/competitions/{competitionId}/results")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PublicResultsController(StackMeetDbContext database) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(string competitionId, CancellationToken ct)
    {
        var normalized = competitionId.Trim().ToUpperInvariant();
        if (normalized.Length is < 3 or > 50) return NotFound();

        var competition = await database.Competitions.AsNoTracking()
            .Where(item => item.CompetitionKey == normalized || item.CompetitionCode == normalized)
            .Select(item => new
            {
                item.Id, item.CompetitionCode, item.CompetitionKey, item.CompetitionName,
                item.Venue, item.StartDate, item.EndDate, item.Status, item.ArchivedAt
            })
            .SingleOrDefaultAsync(ct);

        if (competition is null || competition.ArchivedAt is not null
            || string.Equals(competition.Status, "Archived", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        var assets = await database.CompetitionAssets.AsNoTracking()
            .Where(item => item.CompetitionId == competition.Id)
            .Select(item => item.AssetType)
            .ToListAsync(ct);
        var branding = new
        {
            logoUrl = assets.Contains("logo") ? $"/api/public/competitions/{competition.Id}/assets/logo" : null,
            bannerUrl = assets.Contains("banner") ? $"/api/public/competitions/{competition.Id}/assets/banner" : null
        };

        var savedState = await database.CompetitionStates.AsNoTracking()
            .Where(item => item.CompetitionKey == competition.CompetitionKey)
            .Select(item => new { item.JsonData, item.UpdatedAt })
            .SingleOrDefaultAsync(ct);
        if (savedState is null) return NotFound();

        using var stateDocument = JsonDocument.Parse(savedState.JsonData);
        var root = stateDocument.RootElement;
        var stateStackers = PublicStackers(root);
        var divisionSettings = ReadPublicDivisionSettings(root);
        var settings = ReadPublicSettingsForDivision(root);
        var competitionStart = settings.Start ?? competition.StartDate;
        var sqlStackerRows = await database.Stackers.AsNoTracking()
            .Where(item => item.CompetitionId == competition.Id)
            .OrderBy(item => item.StackerCode)
            .Select(item => new
            {
                item.StackerCode, item.FirstName, item.LastName, item.Gender, item.Club,
                item.Country, item.Region, item.CustomDivision, item.IsSpecialStacker, item.BirthDate
            })
            .ToListAsync(ct);
        var sqlStackers = sqlStackerRows.Select(item => new PublicStacker(
            item.StackerCode,
            (item.FirstName + " " + item.LastName).Trim(),
            item.Gender,
            item.Club ?? "Independent",
            item.Country,
            item.Region ?? "",
            PublicStackerDivision(
                item.CustomDivision,
                item.BirthDate,
                competitionStart,
                item.Gender,
                item.IsSpecialStacker,
                divisionSettings,
                settings.SeparateSpecialDivisionsByGender,
                settings.YearBorn),
            item.IsSpecialStacker ? "Yes" : "No")).ToArray();
        var stackers = stateStackers.Length > 0 ? stateStackers : sqlStackers;
        var sqlResults = await database.CompetitionResults.AsNoTracking().Where(item => item.CompetitionId == competition.Id).OrderBy(item => item.Id)
            .Select(item => new { item.PublicId, item.Stage, item.ParticipantType, item.ParticipantCode, item.EventCode, item.AttemptsJson, item.Penalty, item.UpdatedAt }).ToListAsync(ct);
        var resultRows = sqlResults.Select(item => new { id = item.PublicId, stage = item.Stage, type = item.ParticipantType, participant = item.ParticipantCode, @event = item.EventCode, attempts = ParseAttempts(item.AttemptsJson), penalty = item.Penalty }).ToArray();

        return Ok(new
        {
            competition = new
            {
                id = competition.CompetitionCode,
                key = competition.CompetitionKey,
                name = competition.CompetitionName,
                competition.Venue,
                startDate = competition.StartDate,
                endDate = competition.EndDate,
                competition.Status,
                isOfficial = string.Equals(competition.Status, "Closed", StringComparison.OrdinalIgnoreCase)
            },
            branding,
            lastUpdatedAt = sqlResults.Select(item => (DateTime?)item.UpdatedAt).Concat(new[] { (DateTime?)savedState.UpdatedAt }).Max(),
            settings = PublicSettings(root),
            divisions = PublicDivisions(root),
            results = resultRows,
            doubles = PublicDoubles(root),
            relays = PublicRelays(root),
            stackers
        });
    }

    private static object PublicSettings(JsonElement root)
    {
        if (!root.TryGetProperty("settings", out var settings) || settings.ValueKind != JsonValueKind.Object) return new { };
        return new
        {
            name = Text(settings, "name"),
            type = Text(settings, "type"),
            start = Text(settings, "start"),
            end = Text(settings, "end"),
            // Public-only, non-secret settings used by the Results Portal to label preliminary qualifiers.
            advanceIndividuals = Number(settings, "advanceIndividuals"),
            advanceDoubles = Number(settings, "advanceDoubles"),
            advanceCpDoubles = Number(settings, "advanceCpDoubles"),
            advanceRelay = Number(settings, "advanceRelay")
        };
    }

    private static string[] PublicDivisions(JsonElement root)
    {
        if (!root.TryGetProperty("divisions", out var items) || items.ValueKind != JsonValueKind.Array) return [];
        return items.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? "")
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static PublicStacker[] PublicStackers(JsonElement root)
    {
        if (!root.TryGetProperty("stackers", out var items) || items.ValueKind != JsonValueKind.Array) return [];
        return items.EnumerateArray()
            .Select(item => new PublicStacker(
                Text(item, "id"),
                Text(item, "name"),
                Text(item, "gender"),
                Text(item, "org"),
                Text(item, "country"),
                Text(item, "region"),
                Text(item, "division"),
                Text(item, "special")))
            .Where(item => item.Id.Length > 0)
            .ToArray();
    }

    private static PublicDivisionSettings ReadPublicDivisionSettings(JsonElement root)
    {
        if (!root.TryGetProperty("divisionSettings", out var settings) || settings.ValueKind != JsonValueKind.Object)
        {
            return new PublicDivisionSettings();
        }

        return new PublicDivisionSettings
        {
            Combined = NumberArray(settings, "combined").Select(item => (int)item).ToArray(),
            Male = NumberArray(settings, "male").Select(item => (int)item).ToArray(),
            Female = NumberArray(settings, "female").Select(item => (int)item).ToArray(),
            Special = NumberArray(settings, "special").Select(item => (int)item).ToArray()
        };
    }

    private static PublicSettingsForDivision ReadPublicSettingsForDivision(JsonElement root)
    {
        if (!root.TryGetProperty("settings", out var settings) || settings.ValueKind != JsonValueKind.Object)
        {
            return new PublicSettingsForDivision(null, false, false);
        }

        var start = DateOnly.TryParse(Text(settings, "start"), out var parsedStart) ? parsedStart : (DateOnly?)null;
        var separateSpecial = settings.TryGetProperty("separateSpecialDivisionsByGender", out var separateValue)
            && separateValue.ValueKind == JsonValueKind.True;
        var yearBorn = settings.TryGetProperty("ageCalculationMode", out var ageMode) && ageMode.ValueKind == JsonValueKind.String && string.Equals(ageMode.GetString(), "yearBorn", StringComparison.OrdinalIgnoreCase);
        return new PublicSettingsForDivision(start, separateSpecial, yearBorn);
    }

    private static string PublicStackerDivision(
        string? customDivision,
        DateOnly? birthDate,
        DateOnly? competitionStart,
        string gender,
        bool isSpecial,
        PublicDivisionSettings settings,
        bool separateSpecialDivisionsByGender,
        bool yearBorn)
    {
        if (!string.IsNullOrWhiteSpace(customDivision)) return customDivision.Trim();
        var age = AgeOnCompetitionDate(birthDate, competitionStart, yearBorn);
        if (age <= 0) return "";
        return isSpecial
            ? SpecialDivision(age, gender, settings.Special, separateSpecialDivisionsByGender)
            : StandardDivision(age, gender, settings);
    }

    private static int AgeOnCompetitionDate(DateOnly? birthDate, DateOnly? competitionStart, bool yearBorn)
    {
        if (birthDate is null) return 0;
        var eventDate = competitionStart ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (yearBorn) return Math.Max(eventDate.Year - birthDate.Value.Year, 0);
        var age = eventDate.Year - birthDate.Value.Year;
        if (eventDate.Month < birthDate.Value.Month
            || (eventDate.Month == birthDate.Value.Month && eventDate.Day < birthDate.Value.Day))
        {
            age--;
        }
        return Math.Max(age, 0);
    }

    private static string SpecialDivision(int age, string gender, int[] cutoffs, bool separateByGender)
    {
        var baseDivision = FindRangeName(age, cutoffs, "Special");
        if (string.IsNullOrWhiteSpace(baseDivision)) baseDivision = "SS";
        return separateByGender ? $"{baseDivision} {(string.Equals(gender, "F", StringComparison.OrdinalIgnoreCase) ? "F" : "M")}" : baseDivision;
    }

    private static string StandardDivision(int age, string gender, PublicDivisionSettings settings)
    {
        var genderCutoffs = string.Equals(gender, "F", StringComparison.OrdinalIgnoreCase) ? settings.Female : settings.Male;
        var genderLabel = string.Equals(gender, "F", StringComparison.OrdinalIgnoreCase) ? "Female" : "Male";
        var path = genderCutoffs.Select(item => new DivisionPathItem(item, genderLabel))
            .Concat(settings.Combined.Select(item => new DivisionPathItem(item, "Combined")))
            .Where(item => item.Age > 0)
            .OrderBy(item => item.Age)
            .ToArray();
        return FindRangeName(age, path);
    }

    private static string FindRangeName(int age, int[] cutoffs, string fallbackLabel) =>
        FindRangeName(age, cutoffs.OrderBy(item => item).Select(item => new DivisionPathItem(item, fallbackLabel)).ToArray());

    private static string FindRangeName(int age, DivisionPathItem[] path)
    {
        var previous = 0;
        foreach (var item in path.OrderBy(item => item.Age))
        {
            var cutoff = item.Age;
            var start = previous + 1;
            if (age <= cutoff)
            {
                if (item.Label == "Special")
                {
                    if (start <= 4) return $"SS {cutoff} & Under L1";
                    if (start == cutoff) return $"SS {cutoff} L1";
                    return $"SS {start}-{cutoff} L1";
                }

                if (item.Label == "Combined")
                {
                    var standardName = StandardCombinedDivisionName(start, cutoff);
                    if (!string.IsNullOrWhiteSpace(standardName)) return standardName;
                    if (age >= 19)
                    {
                        var adultName = StandardCombinedDivisionName(age, age);
                        if (!string.IsNullOrWhiteSpace(adultName)) return adultName;
                    }
                }

                if (start <= 4) return $"{cutoff} & Under {item.Label}";
                if (start == cutoff) return $"{cutoff} {item.Label}";
                return $"{start}-{cutoff} {item.Label}";
            }
            previous = cutoff;
        }
        return "";
    }

    private static string StandardCombinedDivisionName(int start, int cutoff)
    {
        var names = new List<string>();
        if (start <= 24 && cutoff >= 19) names.Add("Collegiate C");
        if (cutoff >= 25)
        {
            var firstMaster = MasterLevelForAge(Math.Max(start, 25));
            var lastMaster = MasterLevelForAge(cutoff);
            if (firstMaster > 0 && lastMaster > 0)
            {
                names.Add(firstMaster == lastMaster ? $"Masters {firstMaster} C" : $"Masters {firstMaster}-{lastMaster} C");
            }
        }
        return names.Count == 1 ? names[0] : "";
    }

    private static int MasterLevelForAge(int age) => age switch
    {
        >= 25 and <= 34 => 1,
        >= 35 and <= 44 => 2,
        >= 45 and <= 59 => 3,
        >= 60 => 4,
        _ => 0
    };

    private static IEnumerable<object> PublicDoubles(JsonElement root)
    {
        if (!root.TryGetProperty("doubles", out var items) || items.ValueKind != JsonValueKind.Array) return [];
        return items.EnumerateArray().Select(item => (object)new
        {
            id = Text(item, "id"),
            one = Text(item, "one"),
            two = Text(item, "two"),
            name = Text(item, "name"),
            type = Text(item, "type"),
            division = Text(item, "division"),
            customDivision = Text(item, "customDivision"),
            country = Text(item, "country"),
            region = Text(item, "region")
        }).ToArray();
    }

    private static IEnumerable<object> PublicRelays(JsonElement root)
    {
        if (!root.TryGetProperty("relays", out var items) || items.ValueKind != JsonValueKind.Array) return [];
        return items.EnumerateArray().Select(item => (object)new
        {
            id = Text(item, "id"),
            name = Text(item, "name"),
            division = Text(item, "division"),
            timedRelayDivision = Text(item, "timedRelayDivision"),
            country = Text(item, "country"),
            region = Text(item, "region"),
            org = Text(item, "org"),
            members = StringArray(item, "members"),
            one = Text(item, "one"),
            two = Text(item, "two"),
            three = Text(item, "three"),
            four = Text(item, "four"),
            five = Text(item, "five"),
            six = Text(item, "six")
        }).ToArray();
    }

    private sealed record PublicStacker(
        string Id,
        string Name,
        string Gender,
        string Org,
        string Country,
        string Region,
        string Division,
        string Special);

    private sealed record PublicSettingsForDivision(DateOnly? Start, bool SeparateSpecialDivisionsByGender, bool YearBorn);

    private sealed record DivisionPathItem(int Age, string Label);

    private sealed class PublicDivisionSettings
    {
        public int[] Combined { get; init; } = [];
        public int[] Male { get; init; } = [];
        public int[] Female { get; init; } = [];
        public int[] Special { get; init; } = [];
    }

    private static string Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static decimal Number(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetDecimal(out var number) ? number : 0;

    private static decimal[] NumberArray(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
                .Where(entry => entry.ValueKind == JsonValueKind.Number && entry.TryGetDecimal(out _))
                .Select(entry => entry.GetDecimal())
                .ToArray()
            : [];

    private static decimal[] ParseAttempts(string json)
    {
        try { return JsonSerializer.Deserialize<decimal[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static string[] StringArray(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
                .Where(entry => entry.ValueKind == JsonValueKind.String)
                .Select(entry => entry.GetString() ?? "")
                .Where(entry => entry.Length > 0)
                .ToArray()
            : [];
}
