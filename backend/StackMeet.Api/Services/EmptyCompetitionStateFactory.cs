using System.Text.Json;

namespace StackMeet.Api.Services;

public static class EmptyCompetitionStateFactory
{
    public static string Create(string competitionKey, string competitionName, DateOnly startDate, DateOnly endDate)
    {
        var state = new
        {
            settings = new
            {
                name = competitionName,
                competitionKey,
                type = "Sanctioned",
                start = startDate.ToString("yyyy-MM-dd"),
                end = endDate.ToString("yyyy-MM-dd"),
                prelims = "1",
                finals = "1",
                kbsLogo = "No",
                soc = "Yes",
                prelimTimes = "best",
                paperless = "Yes",
                advanceIndividuals = 10,
                advanceDoubles = 6,
                advanceCpDoubles = 5,
                advanceRelay = 0,
                timeSheetInput = "blank",
                language = "en",
                ageCalculationMode = "actual",
                separateSpecialDivisionsByGender = false
            },
            translations = new { },
            leaderboard = new { type = "Divisional Results", stage = "Prelims", bg = "Black", color = "Blue", pause = "8 seconds", limit = 10 },
            awards = new { },
            events = new { Individuals = new[] { "3-3-3", "3-6-3", "Cycle" }, Doubles = new[] { "Cycle" }, TimedRelay = new[] { "3-6-3" }, HeadToHead = new[] { "3-6-3", "Cycle" } },
            divisionSettings = new { combined = Array.Empty<int>(), male = Array.Empty<int>(), female = Array.Empty<int>(), special = Array.Empty<int>(), timedRelay = Array.Empty<int>(), headToHeadRelay = Array.Empty<int>(), custom = Array.Empty<string>() },
            divisions = Array.Empty<string>(),
            stackers = Array.Empty<object>(),
            doubles = Array.Empty<object>(),
            relays = Array.Empty<object>(),
            results = Array.Empty<object>(),
            notifications = Array.Empty<object>(),
            users = Array.Empty<object>(),
            finalQualificationSnapshots = Array.Empty<object>()
        };

        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = false });
    }
}