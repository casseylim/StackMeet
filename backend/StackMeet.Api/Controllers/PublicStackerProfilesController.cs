using Microsoft.AspNetCore.Mvc;
using StackMeet.Api.Activities.SportStacking.Identity;

namespace StackMeet.Api.Controllers;

/// <summary>Returns the privacy-minimized public Sport Stacker career profile.</summary>
[ApiController]
[Route("api/public/stackers/{nadiTrackId}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PublicStackerProfilesController : ControllerBase
{
    private readonly SportStackerCareerProfileService profiles;
    private readonly PublicFinalsPlacementCareerIntegrationService? finalsPlacements;

    // Retain the original constructor for isolated compatibility tests and non-DI callers.
    public PublicStackerProfilesController(SportStackerCareerProfileService profiles)
        : this(profiles, null)
    {
    }

    public PublicStackerProfilesController(
        SportStackerCareerProfileService profiles,
        PublicFinalsPlacementCareerIntegrationService? finalsPlacements)
    {
        this.profiles = profiles;
        this.finalsPlacements = finalsPlacements;
    }

    [HttpGet]
    public async Task<ActionResult<PublicSportStackerCareerProfile>> Get(
        string nadiTrackId,
        CancellationToken ct)
    {
        var profile = await profiles.GetPublicAsync(nadiTrackId, ct);
        if (profile is null) return NotFound();

        if (finalsPlacements is not null)
        {
            var publication = await finalsPlacements.GetPublicEligibleAsync(nadiTrackId, ct);
            if (publication is not null)
            {
                profile = profile with { FinalsPlacements = publication };
            }
        }

        return Ok(profile);
    }
}

/// <summary>Serves the public profile shell without exposing whether a valid ID exists.</summary>
[ApiExplorerSettings(IgnoreApi = true)]
[Route("Stackers/{nadiTrackId}")]
public sealed class StackerProfilePageController(IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet]
    public IActionResult Get(string nadiTrackId)
    {
        if (!NadiTrackIdRules.IsValid(nadiTrackId)) return NotFound();

        Response.Headers.CacheControl = "no-cache, no-store";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        return PhysicalFile(
            Path.Combine(environment.WebRootPath, "profile", "index.html"),
            "text/html; charset=utf-8");
    }
}
