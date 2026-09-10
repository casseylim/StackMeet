using StackMeet.Api.Activities.SportStacking.Identity;

var identities = new[]
{
    Identity(1, "NDT-7K4M2PX", "1396", "William", "Orrell", new DateOnly(1998, 1, 1), "USA", "Speed Stacks", "william@example.com", "+1 (555) 123-4567"),
    Identity(2, "NDT-X8Q3K7M", null, "Ahmad", "Firdaus", new DateOnly(2015, 3, 14), "Malaysia", "Johor Stackers", "ahmad@example.com", "+60 12-345 6789"),
    Identity(3, "NDT-9R5T2KC", null, "Ahmad", "Firdaus", new DateOnly(2014, 6, 20), "Malaysia", "KL Stackers", null, null),
};

Test("exact NADITrack match approves existing link without extra confirmation", () =>
{
    var match = Match(new("NDT-7K4M2PX", null, null, null, null, null, null, null, null));
    var decision = Resolve(match, Request(StackerIdentityResolutionAction.LinkExisting));

    Equal(StackerIdentityResolutionStatus.Approved, decision.Status);
    Equal(StackerIdentityResolutionAction.LinkExisting, decision.Action);
    Equal(1L, decision.SelectedIdentity!.Id);
    Equal(StackerIdentityMatchMethod.NadiTrackId, decision.LinkMatchMethod);
    True(decision.CanProceedToPersistence);
});

Test("exact NADITrack match blocks create-new duplicate", () =>
{
    var match = Match(new("NDT-7K4M2PX", null, null, null, null, null, null, null, null));
    var decision = Resolve(match, Request(StackerIdentityResolutionAction.CreateNew, createNewOverrideConfirmed: true, resolutionNote: "force"));

    Equal(StackerIdentityResolutionStatus.ExactIdentityMustLink, decision.Status);
    False(decision.CanProceedToPersistence);
});

Test("exact NADITrack link rejects a different selected identity", () =>
{
    var match = Match(new("NDT-7K4M2PX", null, null, null, null, null, null, null, null));
    var decision = Resolve(match, Request(StackerIdentityResolutionAction.LinkExisting, selectedNadiTrackId: "NDT-X8Q3K7M"));

    Equal(StackerIdentityResolutionStatus.InvalidSelection, decision.Status);
});

Test("invalid explicit NADITrack lookup blocks all resolution", () =>
{
    var match = Match(new("NDT-0000000", null, null, null, null, null, null, null, null));
    var decision = Resolve(match, Request(StackerIdentityResolutionAction.CreateNew));

    Equal(StackerIdentityResolutionStatus.LookupBlocked, decision.Status);
    False(decision.CanProceedToPersistence);
});

Test("unknown explicit NADITrack lookup cannot silently become a new person", () =>
{
    var match = Match(new("NDT-2BCDEFG", null, null, null, null, null, null, null, null));
    var decision = Resolve(match, Request(StackerIdentityResolutionAction.CreateNew));

    Equal(StackerIdentityResolutionStatus.LookupBlocked, decision.Status);
});

Test("strong candidate requires explicit operator confirmation", () =>
{
    var match = Match(new(null, "1396", null, null, null, null, null, null, null));
    var decision = Resolve(match, Request(StackerIdentityResolutionAction.LinkExisting, selectedNadiTrackId: "NDT-7K4M2PX"));

    Equal(StackerIdentityResolutionStatus.ConfirmationRequired, decision.Status);
    False(decision.CanProceedToPersistence);
});

Test("confirmed strong candidate is approved with evidence provenance", () =>
{
    var match = Match(new(null, "1396", null, null, null, null, null, null, null));
    var decision = Resolve(match, Request(
        StackerIdentityResolutionAction.LinkExisting,
        selectedNadiTrackId: "NDT-7K4M2PX",
        candidateConfirmed: true));

    Equal(StackerIdentityResolutionStatus.Approved, decision.Status);
    Equal(1L, decision.SelectedIdentity!.Id);
    Equal(StackerIdentityMatchMethod.WssaId, decision.LinkMatchMethod);
});

Test("possible candidate requires a review note even after confirmation", () =>
{
    var match = Match(new(null, null, "Ahmad", "Firdaus", null, "Malaysia", "Johor Stackers", null, null));
    var decision = Resolve(match, Request(
        StackerIdentityResolutionAction.LinkExisting,
        selectedNadiTrackId: "NDT-X8Q3K7M",
        candidateConfirmed: true));

    Equal(StackerIdentityResolutionStatus.ResolutionNoteRequired, decision.Status);
});

Test("confirmed possible candidate with note is approved as manual resolution", () =>
{
    var match = Match(new(null, null, "Ahmad", "Firdaus", null, "Malaysia", "Johor Stackers", null, null));
    var decision = Resolve(match, Request(
        StackerIdentityResolutionAction.LinkExisting,
        selectedNadiTrackId: "NDT-X8Q3K7M",
        candidateConfirmed: true,
        resolutionNote: "Organizer verified athlete details."));

    Equal(StackerIdentityResolutionStatus.Approved, decision.Status);
    Equal(2L, decision.SelectedIdentity!.Id);
    Equal(StackerIdentityMatchMethod.Manual, decision.LinkMatchMethod);
});

Test("cannot link an identity that was not returned as a candidate", () =>
{
    var match = Match(new(null, null, "Ahmad", "Firdaus", null, null, null, null, null));
    var decision = Resolve(match, Request(
        StackerIdentityResolutionAction.LinkExisting,
        selectedNadiTrackId: "NDT-7K4M2PX",
        candidateConfirmed: true,
        resolutionNote: "wrong candidate"));

    Equal(StackerIdentityResolutionStatus.InvalidSelection, decision.Status);
});

Test("no candidates allows normal create-new path", () =>
{
    var match = Match(new(null, null, "Completely", "Different", null, null, null, null, null));
    var decision = Resolve(match, Request(StackerIdentityResolutionAction.CreateNew));

    Equal(StackerIdentityResolutionStatus.Approved, decision.Status);
    Equal(StackerIdentityResolutionAction.CreateNew, decision.Action);
    Equal(StackerIdentityMatchMethod.CreatedNew, decision.LinkMatchMethod);
});

Test("candidate presence requires a dedicated create-new override", () =>
{
    var match = Match(new(null, null, "Ahmad", "Firdaus", null, null, null, null, null));
    var decision = Resolve(match, Request(
        StackerIdentityResolutionAction.CreateNew,
        candidateConfirmed: true,
        resolutionNote: "Candidate confirmation must not double as duplicate override."));

    Equal(StackerIdentityResolutionStatus.DuplicateOverrideRequired, decision.Status);
});

Test("create-new duplicate override requires an audit note", () =>
{
    var match = Match(new(null, null, "Ahmad", "Firdaus", null, null, null, null, null));
    var decision = Resolve(match, Request(
        StackerIdentityResolutionAction.CreateNew,
        createNewOverrideConfirmed: true));

    Equal(StackerIdentityResolutionStatus.ResolutionNoteRequired, decision.Status);
});

Test("explicit override and note allow a distinct new identity despite candidates", () =>
{
    var match = Match(new(null, null, "Ahmad", "Firdaus", null, null, null, null, null));
    var decision = Resolve(match, Request(
        StackerIdentityResolutionAction.CreateNew,
        createNewOverrideConfirmed: true,
        resolutionNote: "Organizer confirmed these are two different people."));

    Equal(StackerIdentityResolutionStatus.Approved, decision.Status);
    Equal(StackerIdentityResolutionAction.CreateNew, decision.Action);
    Equal(StackerIdentityMatchMethod.CreatedNew, decision.LinkMatchMethod);
});

Test("resolution action must be explicit", () =>
{
    var match = Match(new(null, null, "Completely", "Different", null, null, null, null, null));
    var decision = Resolve(match, Request(StackerIdentityResolutionAction.None));

    Equal(StackerIdentityResolutionStatus.ActionRequired, decision.Status);
});

Test("duplicate permanent IDs inside candidate results fail as integrity error", () =>
{
    var duplicates = new[]
    {
        Identity(10, "NDT-7K4M2PX", null, "Same", "Name", null, "Malaysia", null, null, null),
        Identity(11, "NDT-7K4M2PX", null, "Same", "Name", null, "Malaysia", null, null, null),
    };
    var match = StackerIdentityMatcher.FindMatches(
        new(null, null, "Same", "Name", null, null, null, null, null),
        duplicates);

    Throws<InvalidOperationException>(() => Resolve(match, Request(StackerIdentityResolutionAction.CreateNew)));
});

Console.WriteLine("SP-0C stacker identity resolution tests passed.");

StackerIdentityMatchResult Match(StackerIdentityMatchQuery query) =>
    StackerIdentityMatcher.FindMatches(query, identities);

static StackerIdentityResolutionDecision Resolve(
    StackerIdentityMatchResult match,
    StackerIdentityResolutionRequest request) =>
    StackerIdentityResolutionPolicy.Resolve(match, request);

static StackerIdentityResolutionRequest Request(
    StackerIdentityResolutionAction action,
    string? selectedNadiTrackId = null,
    bool candidateConfirmed = false,
    bool createNewOverrideConfirmed = false,
    string? resolutionNote = null) =>
    new(action, selectedNadiTrackId, candidateConfirmed, createNewOverrideConfirmed, resolutionNote);

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
    string? phone) =>
    new()
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
        Console.WriteLine($"FAIL: {name}: {ex.Message}");
        throw;
    }
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}

static void True(bool value)
{
    if (!value) throw new InvalidOperationException("Expected true, got false.");
}

static void False(bool value)
{
    if (value) throw new InvalidOperationException("Expected false, got true.");
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

    throw new InvalidOperationException($"Expected exception {typeof(TException).Name}.");
}
