namespace StackMeet.Api.Activities;

/// <summary>
/// Compatibility module descriptor for the existing Sport Stacking application.
/// This class intentionally contains metadata only; existing Sport Stacking rules
/// continue to live in their current tested implementation until later adapter phases.
/// </summary>
public sealed class SportStackingActivityModule : IActivityModule
{
    public const string ModuleCode = "sport-stacking";

    public string Code => ModuleCode;
    public string DisplayName => "Sport Stacking";
    public string Version => "1";

    public ActivityModuleCapabilities Capabilities { get; } = new(
        SupportsTeamEntries: true,
        SupportsCategories: true,
        SupportsStages: true,
        SupportsLiveResults: true,
        SupportsCertificates: true,
        SupportsOfflinePackage: true);
}
