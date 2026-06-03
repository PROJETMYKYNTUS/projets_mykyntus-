using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ParrainageBackend.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/parrainage")]
public sealed class HealthController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { message = "parrainage-service backend reachable" });

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "healthy", service = "parrainage-service" });
}
