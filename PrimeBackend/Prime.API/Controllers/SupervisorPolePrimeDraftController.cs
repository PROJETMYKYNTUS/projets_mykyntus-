using MediatR;
using Microsoft.AspNetCore.Mvc;
using Prime.Application;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Application.Drafts;

namespace Prime.API.Controllers;

[ApiController]
[Route("api/prime/supervisor-cellule-prime-drafts")]
[Route("api/prime/supervisor-pole-prime-drafts")]
public sealed class SupervisorCellulePrimeDraftController(
    IMediator mediator,
    ISupervisorCellulePrimeDraftAppService? drafts) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SupervisorCellulePrimeDraftResponseDto>> Get(
        [FromQuery] string supervisorUserId,
        [FromQuery] string? celluleId,
        [FromQuery] string? poleId,
        [FromQuery] string period,
        [FromQuery] string templateId,
        CancellationToken ct)
    {
        if (drafts is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            var result = await mediator.Send(
                new GetSupervisorCellulePrimeDraftQuery(supervisorUserId, celluleId, poleId, period, templateId), ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }

    [HttpGet("list-active")]
    public async Task<ActionResult<List<SupervisorCellulePrimeDraftListItemDto>>> ListActive(
        [FromQuery] string supervisorUserId,
        CancellationToken ct)
    {
        if (drafts is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new ListActiveSupervisorCellulePrimeDraftsQuery(supervisorUserId), ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut]
    public async Task<ActionResult<SupervisorCellulePrimeDraftResponseDto>> Upsert(
        [FromBody] UpsertSupervisorCellulePrimeDraftRequest body,
        CancellationToken ct)
    {
        if (drafts is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new UpsertSupervisorCellulePrimeDraftCommand(body), ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (PrimeApiException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromQuery] string supervisorUserId,
        CancellationToken ct)
    {
        if (drafts is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            await mediator.Send(new DeleteSupervisorCellulePrimeDraftCommand(id, supervisorUserId), ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }
}
