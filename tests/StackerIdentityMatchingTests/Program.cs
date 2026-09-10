using StackMeet.Api.Activities.SportStacking.Identity;

var identities = new[]
{
    Identity(1, "NDT-7K4M2PX", "1396", "William", "Orrell", new DateOnly(1998, 1, 1), "USA", "Speed Stacks", "william@example.com", "+1 (555) 123-4567"),
    Identity(2, "NDT-X8Q3K7M", null, "Ahmad", "Firdaus", new DateOnly(2015, 3, 14), "Malaysia", "Johor Stackers", "ahmad@example.com", "+60 12-345 6789"),
    Identity(3, "NDT-9R5T2KC", null, "Ahmad", "Firdaus", new DateOnly(2014, 6, 20), "Malaysia", "KL Stackers", null, null),
};

Test("explicit NADITrack ID is authoritative", () =>
{
    var result = Match(new("  ndt-7k4m2px  ", null, "Wrong", "Name", null, null, null, null, null));
    Equal(StackerIdentityLookupStatus.ExactNadiTrackIdMatch, result.Status);
    True(result.AuthoritativeMatch is not null);
    Equal(1L, result.AuthoritativeMatch!.Identity.Id);
    Equal(StackerIdentityMatchStrength.Authoritative, result.AuthoritativeMatch.Strength);
    True(result.AuthoritativeMatch.IsAuthoritativeSelection);
    False(result.RequiresOperatorConfirmation);
});

Test("invalid explicit NADITrack ID fails closed", () =>
{
    var result = Match(new("NDT-0000000", null, "Ahmad", "Firdaus", new DateOnly(2015, 3, 14), "Malaysia", "Johor Stackers", null, null));
    Equal(StackerIdentityLookupStatus.InvalidNadiTrackId, result.Status);
    Equal(0, result.Candidates.Count);
});

Test("unknown valid explicit NADITrack ID fails closed", () =>
{
    var result = Match(new("NDT-2BCDEFG", null, "Ahmad", "Firdaus", new DateOnly(2015, 3, 14), "Malaysia", "Johor Stackers", null, null));
    Equal(StackerIdentityLookupStatus.NadiTrackIdNotFound, result.Status);
    Equal(0, result.Candidates.Count);
});

Test("WSSA external ID is strong but requires confirmation", () =>
{
    var result = Match(new(null, "1396", null, null, null, null, null, null, null));
    Equal(StackerIdentityLookupStatus.CandidateSearch, result.Status);
    Equal(1, result.Candidates.Count);
    Equal(StackerIdentityMatchStrength.Strong, result.Candidates[0].Strength);
    False(result.Candidates[0].IsAuthoritativeSelection);
    True(result.RequiresOperatorConfirmation);
});

Test("exact name and birth date is strong", () =>
{
    var result = Match(new(null, null, "  ahmad ", " FIRDAUS ", new DateOnly(2015, 3, 14), null, null, null, null));
    Equal(2, result.Candidates.Count);
    Equal(2L, result.Candidates[0].Identity.Id);
    Equal(StackerIdentityMatchStrength.Strong, result.Candidates[0].Strength);
    Equal(StackerIdentityMatchStrength.Possible, result.Candidates[1].Strength);
    True(result.RequiresOperatorConfirmation);
});

Test("email is strong and case insensitive", () =>
{
    var result = Match(new(null, null, null, null, null, null, null, "AHMAD@EXAMPLE.COM", null));
    Equal(1, result.Candidates.Count);
    Equal(2L, result.Candidates[0].Identity.Id);
    Equal(StackerIdentityMatchStrength.Strong, result.Candidates[0].Strength);
});

Test("phone comparison ignores formatting", () =>
{
    var result = Match(new(null, null, null, null, null, null, null, null, "+60123456789"));
    Equal(1, result.Candidates.Count);
    Equal(2L, result.Candidates[0].Identity.Id);
    Equal(StackerIdentityMatchStrength.Strong, result.Candidates[0].Strength);
});

Test("name country club is possible only", () =>
{
    var result = Match(new(null, null, "Ahmad", "Firdaus", null, "Malaysia", "Johor Stackers", null, null));
    Equal(2, result.Candidates.Count);
    Equal(2L, result.Candidates[0].Identity.Id);
    Equal(StackerIdentityMatchStrength.Possible, result.Candidates[0].Strength);
    False(result.Candidates[0].IsAuthoritativeSelection);
});

Test("name alone can discover multiple candidates but never select one", () =>
{
    var result = Match(new(null, null, "Ahmad", "Firdaus", null, null, null, null, null));
    Equal(2, result.Candidates.Count);
    True(result.Candidates.All(candidate => candidate.Strength == StackerIdentityMatchStrength.Possible));
    True(result.Candidates.All(candidate => !candidate.IsAuthoritativeSelection));
    True(result.AuthoritativeMatch is null);
});

Test("no useful evidence returns no candidates", () =>
{
    var result = Match(new(null, null, "Completely", "Different", null, null, null, null, null));
    Equal(StackerIdentityLookupStatus.CandidateSearch, result.Status);
    Equal(0, result.Candidates.Count);
    False(result.RequiresOperatorConfirmation);
});

Test("duplicate permanent NADITrack IDs are an integrity failure", () =>
{
    var duplicate = identities.Concat(new[]
    {
        Identity(4, "NDT-7K4M2PX", null, "Other", "Person", null, "USA", null, null, null),
    });

    Throws<InvalidOperationException>(() =>
        StackerIdentityMatcher.FindMatches(
            new("NDT-7K4M2PX", null, null, null, null, null, null, null, null),
            duplicate));
});

Console.WriteLine("SP-0B stacker identity matching tests passed.");
return;

StackerIdentityMatchResult Match(StackerIdentityMatchQuery query) =>
    StackerIdentityMatcher.FindMatches(query, identities);

static SportStackerIdentity Identity(
    long id,
    string nadiTrackId,
    string? wssaId,
    string firstName,
    string lastName,
    DateOnly? birthDate,
    string country,
    string? club,
    string? email,
    string? phone) => new()
{
    Id = id,
    NadiTrackId = nadiTrackId,
    WssaId = wssaId,
    FirstName = firstName,
    LastName = lastName,
    BirthDate = birthDate,
    Country = country,
    Club = club,
    Email = email,
    Phone = phone,
};

static void Test(string name, Action action)
{
    try
    {
        action();
        Console.WriteLine($"PASS: {name}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"FAIL: {name}: {ex.Message}");
        Environment.ExitCode = 1;
        throw;
    }
}

static void True(bool condition)
{
    if (!condition) throw new InvalidOperationException("Expected true.");
}

static void False(bool condition)
{
    if (condition) throw new InvalidOperationException("Expected false.");
}

static void Equal<T>(T expected, T actual) where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
}

static void Throws<TException>(Action action) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}
