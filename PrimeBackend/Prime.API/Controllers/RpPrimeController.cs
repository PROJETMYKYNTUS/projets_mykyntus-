using MediatR;
using Microsoft.AspNetCore.Mvc;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Application.Rp;
using Prime.Domain.Entities;

namespace Prime.API.Controllers;

[ApiController]
[Route("api/rp")]
public sealed class RpPrimeController(IMediator mediator, IPrimeRpAppService? rp) : ControllerBase
{
    [HttpGet("assigned-project-ids")]
    public async Task<ActionResult<List<string>>> GetAssignedProjectIds([FromQuery] string rpUserId, CancellationToken ct)
    {
        if (rp is null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await mediator.Send(new GetRpAssignedProjectIdsQuery(rpUserId), ct));
    }

    [HttpGet("dashboard-stats")]
    public async Task<ActionResult<ChefProjetDashboardStats>> GetChefProjetDashboardStats([FromQuery] string rpUserId, CancellationToken ct)
    {
        if (rp is null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await mediator.Send(new GetRpDashboardStatsQuery(rpUserId), ct));
    }

    [HttpGet("team-performance")]
    public async Task<ActionResult<List<ChefProjetTeamMemberPerformance>>> GetTeamPerformanceByProject([FromQuery] string rpUserId, CancellationToken ct)
    {
        if (rp is null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await mediator.Send(new GetRpTeamPerformanceQuery(rpUserId), ct));
    }

    [HttpGet("manager-validated")]
    public async Task<ActionResult<List<ChefProjetValidationItem>>> GetSuperviseurValidatedPrimes([FromQuery] string rpUserId, CancellationToken ct)
    {
        if (rp is null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await mediator.Send(new GetRpManagerValidatedQuery(rpUserId), ct));
    }

    [HttpPut("validations/{id}/status")]
    public async Task<ActionResult<ChefProjetValidationItem>> UpdateRpValidationStatus(
        string id,
        [FromBody] UpdateChefProjetValidationStatusRequest req,
        [FromQuery] string? rpUserId,
        CancellationToken ct)
    {
        if (rp is null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(rpUserId))
            return BadRequest(new { error = "rpUserId requis." });
        try
        {
            return Ok(await mediator.Send(new UpdateRpValidationStatusCommand(id, req, rpUserId), ct));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
