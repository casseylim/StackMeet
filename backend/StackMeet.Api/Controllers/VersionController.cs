using Microsoft.AspNetCore.Mvc;

namespace StackMeet.Api.Controllers;

[ApiController]
[Route("api/version")]
public sealed class VersionController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        version = "0.9-online",
        framework = ".NET 8",
        storage = "SQL Server"
    });
}
