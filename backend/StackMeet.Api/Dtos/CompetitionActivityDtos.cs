namespace StackMeet.Api.Dtos;

public sealed record ActivityCapabilitiesResponse(
    bool SupportsTeamEntries,
    bool SupportsCategories,
    bool SupportsStages,
    bool SupportsLiveResults,
    bool SupportsCertificates,
    bool SupportsOfflinePackage);

public sealed record CompetitionActivityResponse(
    string Code,
    string DisplayName,
    string Version,
    ActivityCapabilitiesResponse Capabilities);
