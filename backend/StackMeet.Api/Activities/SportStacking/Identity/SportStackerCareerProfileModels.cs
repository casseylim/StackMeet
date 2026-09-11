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
/// One finalized public tournament performance for an individual event.
/// This is competition-history data only; no private registration attributes are exposed.
/// </summary>
public sealed record SportStackerTournamentPerformance(
    string EventCode,
    decimal OfficialTime,
    decimal RawBestTime,
    decimal AppliedPenalty,
    string Stage);

/// <summary>
/// One finalized, publicly listed competition appearance in the athlete's career history.
/// A competition is retained even when there is no valid individual result for that appearance.
/// </summary>
public sealed record SportStackerTournamentHistory(
    string CompetitionKey,
    string CompetitionName,
    DateOnly CompetitionDate,
    IReadOnlyList<SportStackerTournamentPerformance> Performances);

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
    IReadOnlyList<SportStackerTournamentHistory> TournamentHistory,
    IReadOnlyList<SportStackerPersonalBest> PersonalBests);
