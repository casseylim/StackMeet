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
/// One chronological finalized public performance point for a supported Individual event.
/// PersonalBestAfter is the athlete's best finalized public time through this point in time.
/// </summary>
public sealed record SportStackerCareerProgressPoint(
    string CompetitionKey,
    string CompetitionName,
    DateOnly CompetitionDate,
    decimal OfficialTime,
    string Stage,
    bool IsNewPersonalBest,
    decimal PersonalBestAfter,
    decimal? ImprovementFromPreviousBest);

/// <summary>
/// Chronological public performance progression for one supported Sport Stacking event.
/// </summary>
public sealed record SportStackerEventProgression(
    string EventCode,
    IReadOnlyList<SportStackerCareerProgressPoint> Points);

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
    IReadOnlyList<SportStackerEventProgression> CareerProgression,
    IReadOnlyList<SportStackerPersonalBest> PersonalBests);
