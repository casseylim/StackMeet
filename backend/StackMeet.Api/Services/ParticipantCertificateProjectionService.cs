using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Dtos;
using System.Text.Json;

namespace StackMeet.Api.Services;

public sealed class ParticipantCertificateProjectionService(StackMeetDbContext database)
{
    public async Task<ParticipantCertificateProjection?> Resolve(int competitionId, string participantCode, CancellationToken ct)
    {
        var stacker = await database.Stackers.AsNoTracking().SingleOrDefaultAsync(item => item.CompetitionId == competitionId && item.StackerCode == participantCode, ct);
        if (stacker is null) return null;
        var competition = await database.Competitions.AsNoTracking().SingleAsync(item => item.Id == competitionId, ct);
        var state = await database.CompetitionStates.AsNoTracking().SingleOrDefaultAsync(item => item.CompetitionKey == competition.CompetitionKey, ct);
        var name = string.Join(" ", new[] { stacker.FirstName, stacker.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
        return new ParticipantCertificateProjection(
            stacker.StackerCode,
            string.IsNullOrWhiteSpace(name) ? stacker.StackerCode : name,
            stacker.Club ?? "Independent",
            ResolveDivision(stacker, competition, state?.JsonData),
            stacker.Gender,
            stacker.Country ?? "",
            stacker.Region ?? "",
            stacker.IsSpecialStacker);
    }

    static string ResolveDivision(Models.Stacker stacker, Models.Competition competition, string? json)
    {
        if (!string.IsNullOrWhiteSpace(stacker.CustomDivision)) return stacker.CustomDivision.Trim();
        if (string.IsNullOrWhiteSpace(json)) return "Open / Unassigned";
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var settings = root.TryGetProperty("divisionSettings", out var ds) ? ds : default;
            var combined = Numbers(settings, "combined");
            var male = Numbers(settings, "male");
            var female = Numbers(settings, "female");
            var special = Numbers(settings, "special");
            var config = root.TryGetProperty("settings", out var s) ? s : default;
            DateOnly? start = competition.StartDate;
            if (config.ValueKind == JsonValueKind.Object && config.TryGetProperty("start", out var startValue) && DateOnly.TryParse(startValue.GetString(), out var parsed)) start = parsed;
            var yearBorn = config.ValueKind == JsonValueKind.Object && config.TryGetProperty("ageCalculationMode", out var mode) && string.Equals(mode.GetString(), "yearBorn", StringComparison.OrdinalIgnoreCase);
            var separate = config.ValueKind == JsonValueKind.Object && config.TryGetProperty("separateSpecialDivisionsByGender", out var split) && split.ValueKind == JsonValueKind.True;
            var age = Age(stacker.BirthDate, start, yearBorn);
            if (age <= 0) return "Open / Unassigned";
            if (stacker.IsSpecialStacker)
            {
                var label = Range(age, special, "Special");
                if (string.IsNullOrWhiteSpace(label)) label = "SS";
                return separate ? $"{label} {(string.Equals(stacker.Gender, "F", StringComparison.OrdinalIgnoreCase) ? "F" : "M")}" : label;
            }
            var gender = string.Equals(stacker.Gender, "F", StringComparison.OrdinalIgnoreCase);
            var path = (gender ? female : male).Select(x => (Age: x, Label: gender ? "Female" : "Male")).Concat(combined.Select(x => (Age: x, Label: "Combined"))).Where(x => x.Age > 0).OrderBy(x => x.Age).ToArray();
            return Range(age, path) ?? "Open / Unassigned";
        }
        catch (JsonException) { return "Open / Unassigned"; }
    }

    static int[] Numbers(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array ? array.EnumerateArray().Where(x => x.TryGetInt32(out _)).Select(x => x.GetInt32()).ToArray() : [];
    static int Age(DateOnly? birth, DateOnly? start, bool yearBorn)
    {
        if (birth is null) return 0; var date = start ?? DateOnly.FromDateTime(DateTime.UtcNow); var age = date.Year - birth.Value.Year;
        if (!yearBorn && (date.Month < birth.Value.Month || date.Month == birth.Value.Month && date.Day < birth.Value.Day)) age--; return Math.Max(age, 0);
    }
    static string? Range(int age, int[] cutoffs, string label) => Range(age, cutoffs.OrderBy(x => x).Select(x => (Age: x, Label: label)).ToArray());
    static string? Range(int age, (int Age, string Label)[] path)
    {
        var previous = 0; foreach (var item in path) { var start = previous + 1; if (age <= item.Age) { if (item.Label == "Special") return start <= 4 ? $"SS {item.Age} & Under L1" : start == item.Age ? $"SS {item.Age} L1" : $"SS {start}-{item.Age} L1"; if (start <= 4) return $"{item.Age} & Under {item.Label}"; if (start == item.Age) return $"{item.Age} {item.Label}"; return $"{start}-{item.Age} {item.Label}"; } previous = item.Age; } return null;
    }
}
