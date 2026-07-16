using Microsoft.AspNetCore.Mvc;

namespace StackMeet.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "ok",
        service = "StackMeet.Api",
        version = "0.9-online"
    });
}
