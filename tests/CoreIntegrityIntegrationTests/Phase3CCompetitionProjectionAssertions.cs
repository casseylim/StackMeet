using System.Reflection;
using System.Runtime.CompilerServices;
using StackMeet.Api.Activities;
using StackMeet.Api.Controllers;
using StackMeet.Api.Dtos;
using StackMeet.Api.Models;

internal static class Phase3CCompetitionProjectionAssertions
{
    [ModuleInitializer]
    internal static void Run()
    {
        var createdAt = new DateTime(2026, 8, 1, 2, 3, 4, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 8, 2, 5, 6, 7, DateTimeKind.Utc);
        var competition = new Competition
        {
            Id = 314,
            CompetitionCode = "PHASE3C",
            CompetitionKey = "PHASE3C",
            CompetitionName = "Phase 3C Characterization",
            Venue = "Compatibility Arena",
            StartDate = new DateOnly(2026, 9, 5),
            EndDate = new DateOnly(2026, 9, 6),
            Status = "Active",
            IsPubliclyListed = true,
            ResultsRevision = 27,
            PasswordHash = "not-projected",
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

        var sportStacking = new SportStackingActivityModule();
        var registry = new ActivityModuleRegistry(new IActivityModule[] { sportStacking });
        var resolver = new CompetitionActivityResolver(registry);
        var resolved = resolver.Resolve(competition);
        Assert(ReferenceEquals(resolved, sportStacking), "Phase 3C existing competition resolves to Sport Stacking compatibility module");
        Assert(resolved.Code == SportStackingActivityModule.ModuleCode, "Phase 3C compatibility module code remains sport-stacking");

        var map = typeof(CompetitionsController).GetMethod(
            "Map",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(Competition) },
            modifiers: null) ?? throw new InvalidOperationException("Phase 3C characterization could not find competition response projection.");
        var projected = map.Invoke(null, new object[] { competition }) as CompetitionResponse
            ?? throw new InvalidOperationException("Phase 3C competition response projection returned an unexpected value.");

        Assert(projected.Id == competition.Id, "Phase 3C projection preserves id");
        Assert(projected.CompetitionCode == competition.CompetitionCode, "Phase 3C projection preserves competition code");
        Assert(projected.CompetitionName == competition.CompetitionName, "Phase 3C projection preserves competition name");
        Assert(projected.Venue == competition.Venue, "Phase 3C projection preserves venue");
        Assert(projected.StartDate == competition.StartDate, "Phase 3C projection preserves start date");
        Assert(projected.EndDate == competition.EndDate, "Phase 3C projection preserves end date");
        Assert(projected.Status == competition.Status, "Phase 3C projection preserves status");
        Assert(projected.IsPubliclyListed == competition.IsPubliclyListed, "Phase 3C projection preserves public listing flag");
        Assert(projected.CreatedAt == competition.CreatedAt, "Phase 3C projection preserves created timestamp");
        Assert(projected.UpdatedAt == competition.UpdatedAt, "Phase 3C projection preserves updated timestamp");

        Console.WriteLine("Phase 3C competition projection characterization passed.");
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException($"Failed scenario: {name}");
        Console.WriteLine($"PASS {name}");
    }
}
