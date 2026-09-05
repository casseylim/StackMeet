namespace StackMeet.Api.Activities;

/// <summary>
/// Activity-neutral metadata exposed by a competition activity module.
/// Competition rules, scoring, ranking, registration extensions, and report logic
/// remain owned by the activity implementation rather than Shared Core.
/// </summary>
public interface IActivityModule
{
    string Code { get; }
    string DisplayName { get; }
    string Version { get; }
    ActivityModuleCapabilities Capabilities { get; }
}

/// <summary>
/// Describes broad platform features an activity can participate in without
/// teaching Shared Core any activity-specific rules.
/// </summary>
public sealed record ActivityModuleCapabilities(
    bool SupportsTeamEntries,
    bool SupportsCategories,
    bool SupportsStages,
    bool SupportsLiveResults,
    bool SupportsCertificates,
    bool SupportsOfflinePackage);
