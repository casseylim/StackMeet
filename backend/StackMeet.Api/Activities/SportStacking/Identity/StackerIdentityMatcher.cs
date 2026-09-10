namespace StackMeet.Api.Activities.SportStacking.Identity;

/// <summary>
/// Pure matching policy used before a competition entrant is treated as a new NADITrack person.
/// It discovers candidates but never merges identities. Only an explicitly supplied exact NADITrack ID is authoritative.
/// </summary>
public static class StackerIdentityMatcher
{
    public static StackerIdentityMatchResult FindMatches(
        StackerIdentityMatchQuery query,
        IEnumerable<SportStackerIdentity> identities)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(identities);

        var identityList = identities.ToList();

        if (!string.IsNullOrWhiteSpace(query.NadiTrackId))
        {
            if (!NadiTrackIdRules.IsValid(query.NadiTrackId))
            {
                return Empty(StackerIdentityLookupStatus.InvalidNadiTrackId);
            }

            var normalizedId = NadiTrackIdRules.Normalize(query.NadiTrackId);
            var exactMatches = identityList
                .Where(item => string.Equals(
                    NadiTrackIdRules.Normalize(item.NadiTrackId),
                    normalizedId,
                    StringComparison.Ordinal))
                .ToList();

            if (exactMatches.Count == 0)
            {
                return Empty(StackerIdentityLookupStatus.NadiTrackIdNotFound);
            }

            if (exactMatches.Count > 1)
            {
                throw new InvalidOperationException($"Duplicate permanent NADITrack ID detected: {normalizedId}");
            }

            var authoritative = new StackerIdentityMatchCandidate(
                exactMatches[0],
                StackerIdentityMatchStrength.Authoritative,
                [StackerIdentityMatchMethod.NadiTrackId],
                true);

            return new StackerIdentityMatchResult(
                StackerIdentityLookupStatus.ExactNadiTrackIdMatch,
                authoritative,
                [authoritative]);
        }

        var candidates = identityList
            .Select(item => EvaluateCandidate(query, item))
            .Where(item => item.Strength != StackerIdentityMatchStrength.None)
            .OrderByDescending(item => item.Strength)
            .ThenByDescending(CandidateSpecificity)
            .ThenByDescending(item => item.Evidence.Count)
            .ThenBy(item => item.Identity.NadiTrackId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new StackerIdentityMatchResult(
            StackerIdentityLookupStatus.CandidateSearch,
            null,
            candidates);
    }

    private static StackerIdentityMatchCandidate EvaluateCandidate(
        StackerIdentityMatchQuery query,
        SportStackerIdentity identity)
    {
        var evidence = new List<string>();
        var strong = false;
        var possible = false;

        var sameFullName = SameText(query.FirstName, identity.FirstName)
            && SameText(query.LastName, identity.LastName)
            && HasText(query.FirstName)
            && HasText(query.LastName);

        if (SameText(query.WssaId, identity.WssaId) && HasText(query.WssaId))
        {
            evidence.Add(StackerIdentityMatchMethod.WssaId);
            strong = true;
        }

        if (sameFullName && query.BirthDate.HasValue && identity.BirthDate == query.BirthDate)
        {
            evidence.Add(StackerIdentityMatchMethod.NameAndBirthDate);
            strong = true;
        }

        if (SameEmail(query.Email, identity.Email))
        {
            evidence.Add(StackerIdentityMatchMethod.Email);
            strong = true;
        }

        if (SamePhone(query.Phone, identity.Phone))
        {
            evidence.Add(StackerIdentityMatchMethod.Phone);
            strong = true;
        }

        if (sameFullName)
        {
            if (SameText(query.Country, identity.Country)
                && HasText(query.Country)
                && SameText(query.Club, identity.Club)
                && HasText(query.Club))
            {
                evidence.Add("NAME_COUNTRY_CLUB");
                possible = true;
            }
            else
            {
                evidence.Add("NAME");
                possible = true;
            }
        }

        var strength = strong
            ? StackerIdentityMatchStrength.Strong
            : possible
                ? StackerIdentityMatchStrength.Possible
                : StackerIdentityMatchStrength.None;

        return new StackerIdentityMatchCandidate(identity, strength, evidence, false);
    }

    private static int CandidateSpecificity(StackerIdentityMatchCandidate candidate)
    {
        if (candidate.Evidence.Contains(StackerIdentityMatchMethod.NadiTrackId)) return 100;
        if (candidate.Evidence.Contains(StackerIdentityMatchMethod.WssaId)) return 90;
        if (candidate.Evidence.Contains(StackerIdentityMatchMethod.NameAndBirthDate)) return 80;
        if (candidate.Evidence.Contains(StackerIdentityMatchMethod.Email)) return 75;
        if (candidate.Evidence.Contains(StackerIdentityMatchMethod.Phone)) return 70;
        if (candidate.Evidence.Contains("NAME_COUNTRY_CLUB")) return 20;
        if (candidate.Evidence.Contains("NAME")) return 10;
        return 0;
    }

    private static StackerIdentityMatchResult Empty(StackerIdentityLookupStatus status) =>
        new(status, null, Array.Empty<StackerIdentityMatchCandidate>());

    private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);

    private static bool SameText(string? left, string? right)
    {
        if (!HasText(left) || !HasText(right)) return false;
        return string.Equals(NormalizeText(left!), NormalizeText(right!), StringComparison.Ordinal);
    }

    private static string NormalizeText(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToUpperInvariant();

    private static bool SameEmail(string? left, string? right)
    {
        if (!HasText(left) || !HasText(right)) return false;
        return string.Equals(left!.Trim(), right!.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool SamePhone(string? left, string? right)
    {
        var normalizedLeft = NormalizePhone(left);
        var normalizedRight = NormalizePhone(right);
        return normalizedLeft.Length >= 7
            && normalizedRight.Length >= 7
            && string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal);
    }

    private static string NormalizePhone(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Where(char.IsDigit).ToArray());
}
