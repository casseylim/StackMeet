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

        var savedState = await database.CompetitionStates.AsNoTracking()
            .Where(item => item.CompetitionKey == competition.CompetitionKey)
            .Select(item => new { item.JsonData, item.UpdatedAt })
            .SingleOrDefaultAsync(ct);
        if (savedState is null) return NotFound();

        using var stateDocument = JsonDocument.Parse(savedState.JsonData);
        var root = stateDocument.RootElement;
        var stackers = await database.Stackers.AsNoTracking()
            .Where(item => item.CompetitionId == competition.Id)
            .OrderBy(item => item.StackerCode)
            .Select(item => new
            {
                id = item.StackerCode,
                name = (item.FirstName + " " + item.LastName).Trim(),
                gender = item.Gender,
                org = item.Club ?? "Independent",
                country = item.Country,
                region = item.Region ?? "",
                division = item.CustomDivision ?? "",
                special = item.IsSpecialStacker ? "Yes" : "No"
            })
            .ToListAsync(ct);

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
            lastUpdatedAt = savedState.UpdatedAt,
            settings = PublicSettings(root),
            results = PublicResults(root),
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
            end = Text(settings, "end")
        };
    }

    private static IEnumerable<object> PublicResults(JsonElement root)
    {
        if (!root.TryGetProperty("results", out var items) || items.ValueKind != JsonValueKind.Array) return [];
        return items.EnumerateArray().Select(item => (object)new
        {
            id = Text(item, "id"),
            stage = Text(item, "stage"),
            type = Text(item, "type"),
            participant = Text(item, "participant"),
            @event = Text(item, "event"),
            attempts = NumberArray(item, "attempts"),
            penalty = Number(item, "penalty")
        }).ToArray();
    }

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

    private static string[] StringArray(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
                .Where(entry => entry.ValueKind == JsonValueKind.String)
                .Select(entry => entry.GetString() ?? "")
                .Where(entry => entry.Length > 0)
                .ToArray()
            : [];
}
