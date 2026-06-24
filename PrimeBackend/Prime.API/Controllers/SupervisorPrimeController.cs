using MediatR;
using Microsoft.AspNetCore.Mvc;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Application.LegacyRead;

namespace Prime.API.Controllers;

[ApiController]
[Route("api/supervisor")]
public sealed class SupervisorPrimeController(IMediator mediator, IPrimeAdminReadAppService? admin) : ControllerBase
{
    [HttpGet("primes")]
    public async Task<ActionResult<List<SupervisorPrimeRow>>> GetPrimes(
        [FromQuery] string supervisorUserId, [FromQuery] string? period, CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new GetSupervisorPrimesQuery(supervisorUserId, period), ct));
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (UnauthorizedAccessException e) { return StatusCode(403, new { error = e.Message }); }
    }

    [HttpPost("validate")]
    public async Task<ActionResult<SupervisorPrimeRow>> Validate([FromBody] SupervisorValidateRequest req, CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new SupervisorValidateCommand(req), ct));
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (UnauthorizedAccessException e) { return StatusCode(403, new { error = e.Message }); }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
    }

    [HttpPost("reject")]
    public async Task<ActionResult<SupervisorPrimeRow>> Reject([FromBody] SupervisorRejectRequest req, CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new SupervisorRejectCommand(req), ct));
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (UnauthorizedAccessException e) { return StatusCode(403, new { error = e.Message }); }
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
    }

    [HttpPost("calculate")]
    public async Task<ActionResult<SupervisorCalculateResponse>> Calculate([FromBody] SupervisorCalculateRequest req, CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new SupervisorCalculateCommand(req), ct));
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (UnauthorizedAccessException e) { return StatusCode(403, new { error = e.Message }); }
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<SupervisorDashboardResponse>> GetDashboard([FromQuery] string supervisorUserId, CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new GetSupervisorDashboardQuery(supervisorUserId), ct));
        }
        catch (KeyNotFoundException e) { return NotFound(new { error = e.Message }); }
        catch (UnauthorizedAccessException e) { return StatusCode(403, new { error = e.Message }); }
    }
}
