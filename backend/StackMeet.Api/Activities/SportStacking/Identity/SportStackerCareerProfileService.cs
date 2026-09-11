using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Services;

namespace StackMeet.Api.Activities.SportStacking.Identity;

/// <summary>
/// Read-only public career projection for permanent Sport Stacking identities.
/// Only explicitly public identities and finalized publicly-listed competitions contribute.
/// </summary>
public sealed class SportStackerCareerProfileService(StackMeetDbContext database)
{
    private static readonly string[] EventOrder = ["3-3-3", "3-6-3", "Cycle"];

    public async Task<PublicSportStackerCareerProfile?> GetPublicAsync(
        string? nadiTrackId,
        CancellationToken cancellationToken = default)
    {
        if (!NadiTrackIdRules.IsValid(nadiTrackId)) return null;
        var normalizedId = NadiTrackIdRules.Normalize(nadiTrackId!);

        var identity = await database.SportStackerIdentities
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.NadiTrackId == normalizedId && item.IsPublicProfile,
                cancellationToken);

        // Public callers must not be able to distinguish private from non-existent profiles.
        if (identity is null) return null;

        var appearances = await (
            from link in database.StackerIdentityLinks.AsNoTracking()
            join stacker in database.Stackers.AsNoTracking() on link.StackerId equals stacker.Id
            join competition in database.Competitions.AsNoTracking() on stacker.CompetitionId equals competition.Id
            where link.SportStackerIdentityId == identity.Id
                && competition.IsPubliclyListed
                && (competition.Status == "Closed"
                    || competition.Status == "Archived"
                    || competition.ArchivedAt != null)
            select new CareerAppearanceRow(
                competition.Id,
                competition.CompetitionKey,
                competition.CompetitionName,
                competition.StartDate))
            .Distinct()
            .ToListAsync(cancellationToken);

        var resultRows = await (
            from link in database.StackerIdentityLinks.AsNoTracking()
            join stacker in database.Stackers.AsNoTracking() on link.StackerId equals stacker.Id
            join competition in database.Competitions.AsNoTracking() on stacker.CompetitionId equals competition.Id
            join result in database.CompetitionResults.AsNoTracking()
                on new { stacker.CompetitionId, ParticipantCode = stacker.StackerCode }
                equals new { result.CompetitionId, ParticipantCode = result.ParticipantCode }
            where link.SportStackerIdentityId == identity.Id
                && competition.IsPubliclyListed
                && (competition.Status == "Closed"
                    || competition.Status == "Archived"
                    || competition.ArchivedAt != null)
                && result.ParticipantType == "Individual"
            select new CareerResultRow(
                competition.Id,
                result.Id,
                result.Stage,
                result.EventCode,
                result.AttemptsJson,
                result.Penalty,
                competition.CompetitionKey,
                competition.CompetitionName,
                competition.StartDate))
            .ToListAsync(cancellationToken);

        var candidates = new List<PersonalBestCandidate>();
        foreach (var row in resultRows)
        {
            var candidate = TryCreateCandidate(row);
            if (candidate is not null) candidates.Add(candidate);
        }

        var personalBests = candidates
            .GroupBy(item => item.EventCode, StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(item => item.OfficialTime)
                .ThenBy(item => item.CompetitionDate)
                .ThenBy(item => item.ResultId)
                .First())
            .Select(item => new SportStackerPersonalBest(
                item.EventCode,
                item.OfficialTime,
                item.RawBestTime,
                item.AppliedPenalty,
                item.CompetitionKey,
                item.CompetitionName,
                item.CompetitionDate,
                item.Stage))
            .OrderBy(item => EventSort(item.EventCode))
            .ThenBy(item => item.EventCode, StringComparer.Ordinal)
            .ToList();

        var tournamentHistory = appearances
            .OrderByDescending(item => item.StartDate)
            .ThenByDescending(item => item.CompetitionId)
            .Select(appearance =>
            {
                var performances = candidates
                    .Where(item => item.CompetitionId == appearance.CompetitionId)
                    .GroupBy(item => item.EventCode, StringComparer.Ordinal)
                    .Select(group => group
                        .OrderBy(item => item.OfficialTime)
                        .ThenBy(item => item.ResultId)
                        .First())
                    .Select(item => new SportStackerTournamentPerformance(
                        item.EventCode,
                        item.OfficialTime,
                        item.RawBestTime,
                        item.AppliedPenalty,
                        item.Stage))
                    .OrderBy(item => EventSort(item.EventCode))
                    .ThenBy(item => item.EventCode, StringComparer.Ordinal)
                    .ToList();

                return new SportStackerTournamentHistory(
                    appearance.CompetitionKey,
                    appearance.CompetitionName,
                    appearance.StartDate,
                    performances);
            })
            .ToList();

        var firstCompetitionDate = appearances.Count == 0
            ? (DateOnly?)null
            : appearances.Min(item => item.StartDate);
        var latestCompetitionDate = appearances.Count == 0
            ? (DateOnly?)null
            : appearances.Max(item => item.StartDate);

        return new PublicSportStackerCareerProfile(
            identity.NadiTrackId,
            $"{identity.FirstName.Trim()} {identity.LastName.Trim()}".Trim(),
            identity.Country.Trim(),
            TrimOrNull(identity.Club),
            TrimOrNull(identity.Region),
            appearances.Count,
            firstCompetitionDate,
            latestCompetitionDate,
            tournamentHistory,
            personalBests);
    }

    private static PersonalBestCandidate? TryCreateCandidate(CareerResultRow row)
    {
        var eventCode = CompetitionResultRules.NormalizeEvent(row.EventCode);
        var stage = CompetitionResultRules.NormalizeStage(row.Stage);
        if (eventCode is null || stage is null) return null;

        decimal[] attempts;
        try
        {
            attempts = JsonSerializer.Deserialize<decimal[]>(row.AttemptsJson) ?? [];
        }
        catch (JsonException)
        {
            return null;
        }

        // Mirrors the existing frontend BestResultEngine validity semantics.
        var validAttempts = attempts
            .Where(value => value > 0m && value < 999m)
            .ToArray();
        if (validAttempts.Length == 0) return null;

        var rawBestTime = validAttempts.Min();
        var appliedPenalty = row.Penalty > 0m && row.Penalty < 999m
            ? row.Penalty
            : 0m;

        return new PersonalBestCandidate(
            row.CompetitionId,
            row.ResultId,
            eventCode,
            rawBestTime + appliedPenalty,
            rawBestTime,
            appliedPenalty,
            row.CompetitionKey,
            row.CompetitionName,
            row.CompetitionDate,
            stage);
    }

    private static int EventSort(string eventCode)
    {
        var index = Array.IndexOf(EventOrder, eventCode);
        return index < 0 ? int.MaxValue : index;
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record CareerAppearanceRow(
        int CompetitionId,
        string CompetitionKey,
        string CompetitionName,
        DateOnly StartDate);

    private sealed record CareerResultRow(
        int CompetitionId,
        long ResultId,
        string Stage,
        string EventCode,
        string AttemptsJson,
        decimal Penalty,
        string CompetitionKey,
        string CompetitionName,
        DateOnly CompetitionDate);

    private sealed record PersonalBestCandidate(
        int CompetitionId,
        long ResultId,
        string EventCode,
        decimal OfficialTime,
        decimal RawBestTime,
        decimal AppliedPenalty,
        string CompetitionKey,
        string CompetitionName,
        DateOnly CompetitionDate,
        string Stage);
}
