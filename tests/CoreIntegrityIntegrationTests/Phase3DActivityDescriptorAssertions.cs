using System.Reflection;
using System.Runtime.CompilerServices;
using StackMeet.Api.Activities;
using StackMeet.Api.Controllers;
using StackMeet.Api.Dtos;
using StackMeet.Api.Models;

internal static class Phase3DActivityDescriptorAssertions
{
    [ModuleInitializer]
    internal static void Run()
    {
        var competition = new Competition
        {
            Id = 315,
            CompetitionCode = "PHASE3D",
            CompetitionKey = "PHASE3D",
            CompetitionName = "Phase 3D Descriptor Characterization",
            Venue = "Compatibility Arena",
            StartDate = new DateOnly(2026, 9, 5),
            EndDate = new DateOnly(2026, 9, 6),
            Status = "Active",
            IsPubliclyListed = true,
            CreatedAt = new DateTime(2026, 9, 5, 1, 2, 3, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 9, 5, 4, 5, 6, DateTimeKind.Utc)
        };

        var sportStacking = new SportStackingActivityModule();
        var registry = new ActivityModuleRegistry(new IActivityModule[] { sportStacking });
        var resolver = new CompetitionActivityResolver(registry);
        var resolved = resolver.Resolve(competition);
        Assert(ReferenceEquals(resolved, sportStacking), "Phase 3D existing competition resolves to Sport Stacking compatibility module");
        Assert(resolved.Code == "sport-stacking", "Phase 3D compatibility descriptor code remains sport-stacking");

        var map = typeof(CompetitionsController).GetMethod(
            "MapActivity",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(IActivityModule) },
            modifiers: null) ?? throw new InvalidOperationException("Phase 3D characterization could not find activity descriptor projection.");
        var projected = map.Invoke(null, new object[] { resolved }) as CompetitionActivityResponse
            ?? throw new InvalidOperationException("Phase 3D activity descriptor projection returned an unexpected value.");

        Assert(projected.Code == resolved.Code, "Phase 3D descriptor preserves module code");
        Assert(projected.DisplayName == resolved.DisplayName, "Phase 3D descriptor preserves display name");
        Assert(projected.Version == resolved.Version, "Phase 3D descriptor preserves version");
        Assert(projected.Capabilities.SupportsTeamEntries == resolved.Capabilities.SupportsTeamEntries, "Phase 3D descriptor preserves team capability");
        Assert(projected.Capabilities.SupportsCategories == resolved.Capabilities.SupportsCategories, "Phase 3D descriptor preserves category capability");
        Assert(projected.Capabilities.SupportsStages == resolved.Capabilities.SupportsStages, "Phase 3D descriptor preserves stage capability");
        Assert(projected.Capabilities.SupportsLiveResults == resolved.Capabilities.SupportsLiveResults, "Phase 3D descriptor preserves live-results capability");
        Assert(projected.Capabilities.SupportsCertificates == resolved.Capabilities.SupportsCertificates, "Phase 3D descriptor preserves certificate capability");
        Assert(projected.Capabilities.SupportsOfflinePackage == resolved.Capabilities.SupportsOfflinePackage, "Phase 3D descriptor preserves offline-package capability");

        Console.WriteLine("Phase 3D activity descriptor characterization passed.");
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException($"Failed scenario: {name}");
        Console.WriteLine($"PASS {name}");
    }
}
