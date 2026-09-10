using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Models;

namespace StackMeet.Api.Activities.SportStacking.Identity;

/// <summary>
/// SP-2 historical identity-linking boundary.
/// Discovery is read-only. Apply recomputes current candidates and persists at most one competition Stacker
/// through the reviewed SP-0C resolution policy and SP-1 transactional persistence service.
/// </summary>
public sealed class StackerIdentityBackfillService(
    StackMeetDbContext database,
    INadiTrackIdGenerator idGenerator)
{
    public const int DefaultDiscoveryItems = 100;
    public const int MaximumDiscoveryItems = 500;

    public async Task<StackerIdentityBackfillReport> DiscoverAsync(
        int? competitionId = null,
        int take = DefaultDiscoveryItems,
        CancellationToken cancellationToken = default)
    {
        if (competitionId.HasValue && competitionId.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(competitionId));
        }

        if (take <= 0 || take > MaximumDiscoveryItems)
        {
            throw new ArgumentOutOfRangeException(
                nameof(take),
                $"Discovery take must be between 1 and {MaximumDiscoveryItems}.");
        }

        var scopedStackers = database.Stackers.AsNoTracking().AsQueryable();
        if (competitionId.HasValue)
        {
            scopedStackers = scopedStackers.Where(item => item.CompetitionId == competitionId.Value);
        }

        var totalStackers = await scopedStackers.CountAsync(cancellationToken);
        var linkedStackers = await scopedStackers.CountAsync(
            stacker => database.StackerIdentityLinks.Any(link => link.StackerId == stacker.Id),
            cancellationToken);
        var unlinkedStackers = totalStackers - linkedStackers;

        var stackers = await scopedStackers
            .Where(stacker => !database.StackerIdentityLinks.Any(link => link.StackerId == stacker.Id))
            .OrderBy(stacker => stacker.CompetitionId)
            .ThenBy(stacker => stacker.StackerCode)
            .ThenBy(stacker => stacker.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

        var identities = await database.SportStackerIdentities
            .AsNoTracking()
            .OrderBy(item => item.NadiTrackId)
            .ToListAsync(cancellationToken);

        var items = new List<StackerIdentityBackfillItem>(stackers.Count);
        foreach (var stacker in stackers)
        {
            var matchResult = StackerIdentityMatcher.FindMatches(BuildMatchQuery(stacker, null), identities);
            var candidates = matchResult.Candidates.Select(MapCandidate).ToList();
            var status = candidates.Count == 0
                ? StackerIdentityBackfillItemStatus.NoCandidates
                : StackerIdentityBackfillItemStatus.ReviewRequired;

            items.Add(new StackerIdentityBackfillItem(
                stacker.Id,
                stacker.CompetitionId,
                stacker.StackerCode,
                DisplayName(stacker.FirstName, stacker.LastName),
                stacker.BirthDate,
                stacker.Country,
                TrimOrNull(stacker.Club),
                TrimOrNull(stacker.WssaId),
                status,
                candidates));
        }

        return new StackerIdentityBackfillReport(
            competitionId,
            totalStackers,
            linkedStackers,
            unlinkedStackers,
            items.Count,
            items.Count(item => item.Status == StackerIdentityBackfillItemStatus.ReviewRequired),
            items.Count(item => item.Status == StackerIdentityBackfillItemStatus.NoCandidates),
            unlinkedStackers > items.Count,
            items);
    }

    public async Task<StackerIdentityBackfillApplyResult> ApplyAsync(
        StackerIdentityBackfillApplyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.StackerId <= 0) throw new ArgumentOutOfRangeException(nameof(request.StackerId));

        var existingNadiTrackId = await database.StackerIdentityLinks
            .AsNoTracking()
            .Where(link => link.StackerId == request.StackerId)
            .Select(link => link.SportStackerIdentity.NadiTrackId)
            .SingleOrDefaultAsync(cancellationToken);

        if (existingNadiTrackId is not null)
        {
            return new StackerIdentityBackfillApplyResult(
                StackerIdentityBackfillApplyStatus.AlreadyLinked,
                request.StackerId,
                existingNadiTrackId,
                null,
                null);
        }

        var stacker = await database.Stackers
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.StackerId, cancellationToken)
            ?? throw new KeyNotFoundException($"Competition Stacker {request.StackerId} was not found.");

        var identities = await database.SportStackerIdentities
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Recompute against current persisted data on every apply. Discovery output is informational only
        // and can never be replayed as authority after the underlying identities have changed.
        var matchResult = StackerIdentityMatcher.FindMatches(
            BuildMatchQuery(stacker, TrimOrNull(request.ExplicitNadiTrackId)),
            identities);

        var resolution = StackerIdentityResolutionPolicy.Resolve(
            matchResult,
            new StackerIdentityResolutionRequest(
                request.RequestedAction,
                TrimOrNull(request.SelectedNadiTrackId),
                request.CandidateConfirmed,
                request.CreateNewOverrideConfirmed,
                TrimOrNull(request.ResolutionNote)));

        if (!resolution.CanProceedToPersistence)
        {
            return new StackerIdentityBackfillApplyResult(
                StackerIdentityBackfillApplyStatus.ResolutionBlocked,
                request.StackerId,
                resolution.SelectedIdentity?.NadiTrackId,
                resolution,
                null);
        }

        var persistenceService = new StackerIdentityPersistenceService(database, idGenerator);
        var persistence = await persistenceService.PersistAsync(
            new StackerIdentityPersistenceRequest(
                request.StackerId,
                resolution,
                TrimOrNull(request.ResolutionNote),
                request.LinkedByUserId),
            cancellationToken);

        return new StackerIdentityBackfillApplyResult(
            StackerIdentityBackfillApplyStatus.Applied,
            request.StackerId,
            persistence.Identity.NadiTrackId,
            resolution,
            persistence);
    }

    private static StackerIdentityMatchQuery BuildMatchQuery(Stacker stacker, string? explicitNadiTrackId) =>
        new(
            explicitNadiTrackId,
            stacker.WssaId,
            stacker.FirstName,
            stacker.LastName,
            stacker.BirthDate,
            stacker.Country,
            stacker.Club,
            stacker.Email,
            stacker.Phone);

    private static StackerIdentityBackfillCandidate MapCandidate(StackerIdentityMatchCandidate candidate)
    {
        if (!NadiTrackIdRules.IsValid(candidate.Identity.NadiTrackId))
        {
            throw new InvalidOperationException(
                $"Historical backfill candidate contains invalid NADITrack ID '{candidate.Identity.NadiTrackId}'.");
        }

        return new StackerIdentityBackfillCandidate(
            candidate.Identity.Id,
            NadiTrackIdRules.Normalize(candidate.Identity.NadiTrackId),
            DisplayName(candidate.Identity.FirstName, candidate.Identity.LastName),
            candidate.Identity.BirthDate,
            candidate.Identity.Country,
            TrimOrNull(candidate.Identity.Club),
            TrimOrNull(candidate.Identity.WssaId),
            candidate.Strength,
            candidate.Evidence.ToArray());
    }

    private static string DisplayName(string firstName, string lastName) =>
        $"{firstName.Trim()} {lastName.Trim()}".Trim();

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
