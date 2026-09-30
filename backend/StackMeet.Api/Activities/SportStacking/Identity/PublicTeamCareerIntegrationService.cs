using StackMeet.Api.Data;

namespace StackMeet.Api.Activities.SportStacking.Identity;

/// <summary>
/// SP-4S integration boundary that reuses SP-4Q as the only team-career source and SP-4R as the
/// only public transformation. It adds no team parsing, scoring, placement or identity inference.
/// </summary>
public sealed class PublicTeamCareerIntegrationService(StackMeetDbContext database)
{
    public async Task<PublicTeamCareerPublication?> GetPublicEligibleAsync(
        string? nadiTrackId,
        CancellationToken cancellationToken = default)
    {
        if (!NadiTrackIdRules.IsValid(nadiTrackId)) return null;
        var normalizedId = NadiTrackIdRules.Normalize(nadiTrackId!);

        try
        {
            var source = await new IdentityLinkedTeamCareerService(database)
                .GetPublicEligibleAsync(normalizedId, cancellationToken);

            if (source is null) return null;
            return PublicTeamCareerPublicationContract.Create(source);
        }
        catch (InvalidOperationException)
        {
            // Fail closed for team history while preserving the otherwise valid public profile.
            return Empty(normalizedId);
        }
    }

    private static PublicTeamCareerPublication Empty(string normalizedId) =>
        new(
            PublicTeamCareerPublicationContract.PublicationVersion,
            normalizedId,
            PublicTeamCareerPublicationContract.PrivacyPolicy,
            []);
}
