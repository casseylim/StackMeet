using Microsoft.AspNetCore.Mvc;

namespace StackMeet.Api.Controllers;

// Same /api/admin middleware as every identity mutation; never trusts browser role flags.
[ApiController]
[Route("api/admin/career-profile/capabilities")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminCareerProfileCapabilitiesController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        authenticated = true,
        isSystemAdmin = true,
        canCreateIdentity = true,
        canLinkIdentity = true,
        canPublishProfile = true,
        canUnpublishProfile = true,
        canUnlinkIdentity = true
    });
}
