namespace StackMeet.Api.Activities;

/// <summary>
/// Minimal production descriptor for the Chess activity module.
/// Phase 5A intentionally exposes metadata only. Chess-specific registration,
/// pairing, scoring, standings, reports and UI belong to later module phases.
/// </summary>
public sealed class ChessActivityModule : IActivityModule
{
    public const string ModuleCode = "chess";

    public string Code => ModuleCode;
    public string DisplayName => "Chess";
    public string Version => "1";

    public ActivityModuleCapabilities Capabilities { get; } = new(
        SupportsTeamEntries: false,
        SupportsCategories: false,
        SupportsStages: false,
        SupportsLiveResults: false,
        SupportsCertificates: false,
        SupportsOfflinePackage: false);
}
