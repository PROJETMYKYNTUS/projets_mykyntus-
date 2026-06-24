using MediatR;
using Microsoft.AspNetCore.Mvc;
using Prime.Application;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Application.GlobalPool;

namespace Prime.API.Controllers;

[ApiController]
[Route("api/prime/pilotage")]
public sealed class PrimePilotageController(IMediator mediator, IPrimePilotageAppService? pilotage) : ControllerBase
{
    [HttpGet("cells-summary")]
    public async Task<ActionResult<List<ServicePilotageSummaryDto>>> CellsSummary(
        [FromQuery] string supervisorUserId,
        [FromQuery] string period,
        CancellationToken ct)
    {
        if (pilotage is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new GetPilotageCellsSummaryQuery(supervisorUserId, period), ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
