using System.Data;
using Microsoft.EntityFrameworkCore;
using StackMeet.Api.Data;
using StackMeet.Api.Models;

namespace StackMeet.Api.Activities.SportStacking.Identity;

/// <summary>
/// SP-1 persistence boundary for permanent Sport Stacking identities.
/// It accepts only an approved SP-0C decision and atomically creates the permanent identity/link or links an existing identity.
/// It does not change the competition-scoped Stacker snapshot.
/// </summary>
public sealed class StackerIdentityPersistenceService(
    StackMeetDbContext database,
    INadiTrackIdGenerator idGenerator)
{
    public const int MaxIdIssuanceAttempts = 32;

    public async Task<StackerIdentityPersistenceResult> PersistAsync(
        StackerIdentityPersistenceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Resolution);

        var note = TrimOrNull(request.ResolutionNote);
        ValidateApprovedResolution(request.Resolution, note);

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var stacker = await database.Stackers
            .SingleOrDefaultAsync(item => item.Id == request.StackerId, cancellationToken)
            ?? throw new KeyNotFoundException($"Competition Stacker {request.StackerId} was not found.");

        var links = database.Set<StackerIdentityLink>();
        if (await links.AnyAsync(item => item.StackerId == stacker.Id, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Competition Stacker {stacker.Id} is already linked to a permanent NADITrack identity.");
        }

        var now = DateTime.UtcNow;
        SportStackerIdentity identity;
        var identityCreated = false;

        if (request.Resolution.Action == StackerIdentityResolutionAction.LinkExisting)
        {
            identity = await ResolvePersistedIdentityAsync(request.Resolution, cancellationToken);
        }
        else if (request.Resolution.Action == StackerIdentityResolutionAction.CreateNew)
        {
            identity = await CreateIdentityFromStackerAsync(stacker, now, cancellationToken);
            identityCreated = true;
            database.Set<SportStackerIdentity>().Add(identity);
        }
        else
        {
            throw new InvalidOperationException(
                $"Approved identity resolution has unsupported action {request.Resolution.Action}.");
        }

        var link = new StackerIdentityLink
        {
            SportStackerIdentity = identity,
            Stacker = stacker,
            MatchMethod = request.Resolution.LinkMatchMethod!,
            ResolutionReasonCode = request.Resolution.ReasonCode,
            ResolutionNote = note,
            LinkedAt = now,
            LinkedByUserId = request.LinkedByUserId
        };

        links.Add(link);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new StackerIdentityPersistenceResult(identity, link, identityCreated);
    }

    private async Task<SportStackerIdentity> ResolvePersistedIdentityAsync(
        StackerIdentityResolutionDecision resolution,
        CancellationToken cancellationToken)
    {
        var selected = resolution.SelectedIdentity
            ?? throw new InvalidOperationException("Approved LinkExisting decision is missing its selected identity.");

        if (selected.Id <= 0 || !NadiTrackIdRules.IsValid(selected.NadiTrackId))
        {
            throw new InvalidOperationException("Approved LinkExisting decision contains an invalid persisted identity reference.");
        }

        var persisted = await database.Set<SportStackerIdentity>()
            .SingleOrDefaultAsync(item => item.Id == selected.Id, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Selected permanent identity {selected.Id} no longer exists.");

        if (!NadiTrackIdRules.IsValid(persisted.NadiTrackId)
            || !string.Equals(
                NadiTrackIdRules.Normalize(persisted.NadiTrackId),
                NadiTrackIdRules.Normalize(selected.NadiTrackId),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Selected permanent identity changed between resolution and persistence.");
        }

        return persisted;
    }

    private async Task<SportStackerIdentity> CreateIdentityFromStackerAsync(
        Stacker stacker,
        DateTime now,
        CancellationToken cancellationToken)
    {
        ValidateStackerSnapshot(stacker);

        var identities = database.Set<SportStackerIdentity>();
        string? issuedId = null;

        for (var attempt = 0; attempt < MaxIdIssuanceAttempts; attempt++)
        {
            var generated = idGenerator.Generate();
            if (!NadiTrackIdRules.IsValid(generated))
            {
                throw new InvalidOperationException(
                    $"NADITrack ID generator produced an invalid identifier: '{generated}'.");
            }

            var normalized = NadiTrackIdRules.Normalize(generated);
            if (!await identities.AnyAsync(item => item.NadiTrackId == normalized, cancellationToken))
            {
                issuedId = normalized;
                break;
            }
        }

        if (issuedId is null)
        {
            throw new InvalidOperationException(
                $"Unable to issue a unique NADITrack ID after {MaxIdIssuanceAttempts} attempts.");
        }

        return new SportStackerIdentity
        {
            NadiTrackId = issuedId,
            WssaId = TrimOrNull(stacker.WssaId),
            FirstName = stacker.FirstName.Trim(),
            LastName = stacker.LastName.Trim(),
            Gender = stacker.Gender.Trim(),
            BirthDate = stacker.BirthDate,
            Country = stacker.Country.Trim(),
            Club = TrimOrNull(stacker.Club),
            Region = TrimOrNull(stacker.Region),
            Email = TrimOrNull(stacker.Email),
            Phone = TrimOrNull(stacker.Phone),
            IsPublicProfile = false,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static void ValidateApprovedResolution(
        StackerIdentityResolutionDecision resolution,
        string? resolutionNote)
    {
        if (!resolution.CanProceedToPersistence
            || resolution.Status != StackerIdentityResolutionStatus.Approved)
        {
            throw new InvalidOperationException("Only an SP-0C Approved identity resolution may be persisted.");
        }

        if (string.IsNullOrWhiteSpace(resolution.ReasonCode)
            || resolution.ReasonCode.Length > 100)
        {
            throw new InvalidOperationException("Approved identity resolution has an invalid reason code.");
        }

        if (resolutionNote?.Length > 1000)
        {
            throw new InvalidOperationException("Identity resolution note must be 1000 characters or fewer.");
        }

        if (resolution.Action == StackerIdentityResolutionAction.LinkExisting)
        {
            if (resolution.SelectedIdentity is null)
            {
                throw new InvalidOperationException("Approved LinkExisting decision is missing its selected identity.");
            }

            var method = resolution.LinkMatchMethod;
            if (string.IsNullOrWhiteSpace(method)
                || !AllowedExistingLinkMethods.Contains(method))
            {
                throw new InvalidOperationException("Approved LinkExisting decision has an invalid link match method.");
            }

            if (resolution.ReasonCode == "APPROVED_EXACT_NADITRACK_LINK")
            {
                if (!string.Equals(method, StackerIdentityMatchMethod.NadiTrackId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Exact NADITrack approval must use NADITRACK_ID provenance.");
                }
            }
            else if (resolution.ReasonCode == "APPROVED_CONFIRMED_CANDIDATE_LINK")
            {
                if (string.Equals(method, StackerIdentityMatchMethod.NadiTrackId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Candidate approval cannot claim authoritative NADITrack-ID provenance.");
                }
            }
            else
            {
                throw new InvalidOperationException("Unrecognized approved LinkExisting reason code.");
            }

            if (string.Equals(method, StackerIdentityMatchMethod.Manual, StringComparison.Ordinal)
                && resolutionNote is null)
            {
                throw new InvalidOperationException("Manual identity links require a persisted review note.");
            }

            return;
        }

        if (resolution.Action == StackerIdentityResolutionAction.CreateNew)
        {
            if (resolution.SelectedIdentity is not null
                || !string.Equals(
                    resolution.LinkMatchMethod,
                    StackerIdentityMatchMethod.CreatedNew,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Approved CreateNew decision has inconsistent identity/provenance data.");
            }

            if (resolution.ReasonCode == "APPROVED_CREATE_NEW_DESPITE_CANDIDATES")
            {
                if (resolutionNote is null)
                {
                    throw new InvalidOperationException("Duplicate override creation requires a persisted review note.");
                }
            }
            else if (resolution.ReasonCode != "APPROVED_NO_DUPLICATE_CANDIDATES")
            {
                throw new InvalidOperationException("Unrecognized approved CreateNew reason code.");
            }

            return;
        }

        throw new InvalidOperationException("Approved identity resolution has no persistable action.");
    }

    private static void ValidateStackerSnapshot(Stacker stacker)
    {
        if (string.IsNullOrWhiteSpace(stacker.FirstName)
            || string.IsNullOrWhiteSpace(stacker.LastName)
            || string.IsNullOrWhiteSpace(stacker.Gender)
            || string.IsNullOrWhiteSpace(stacker.Country))
        {
            throw new InvalidOperationException(
                "Competition Stacker snapshot is incomplete and cannot seed a permanent identity.");
        }
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static readonly HashSet<string> AllowedExistingLinkMethods =
    [
        StackerIdentityMatchMethod.NadiTrackId,
        StackerIdentityMatchMethod.WssaId,
        StackerIdentityMatchMethod.NameAndBirthDate,
        StackerIdentityMatchMethod.Email,
        StackerIdentityMatchMethod.Phone,
        StackerIdentityMatchMethod.Manual
    ];
}
