namespace StackMeet.Api.Activities.SportStacking.Identity;

/// <summary>
/// One finalized, public Sport Stacking personal best with enough provenance to explain where it came from.
/// Sensitive registration fields are intentionally absent from this public read model.
/// </summary>
public sealed record SportStackerPersonalBest(
    string EventCode,
    decimal OfficialTime,
    decimal RawBestTime,
    decimal AppliedPenalty,
    string CompetitionKey,
    string CompetitionName,
    DateOnly CompetitionDate,
    string Stage);

/// <summary>
/// Privacy-safe public career projection keyed by the permanent NADITrack ID.
/// Birth date, email, phone, gender and external IDs are deliberately not part of this contract.
/// </summary>
public sealed record PublicSportStackerCareerProfile(
    string NadiTrackId,
    string DisplayName,
    string Country,
    string? Club,
    string? Region,
    int CompetitionCount,
    DateOnly? FirstCompetitionDate,
    DateOnly? LatestCompetitionDate,
    IReadOnlyList<SportStackerPersonalBest> PersonalBests);
