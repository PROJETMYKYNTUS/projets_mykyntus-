using MediatR;
using Microsoft.AspNetCore.Mvc;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Application.Org;

namespace Prime.API.Controllers;

[ApiController]
[Route("api/prime/cellules/{celluleId}/common-line-ponderations")]
public sealed class CelluleCommonLinePonderationsController(
    IMediator mediator,
    ICommonLinePonderationsAppService? ponderations) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<EffectiveCommonLinePonderationDto>>> Get(
        string celluleId,
        [FromQuery] string supervisorUserId,
        [FromQuery] string? templateId,
        [FromQuery] DateTimeOffset? effectiveAt,
        CancellationToken ct)
    {
        if (ponderations is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(
                new GetCelluleCommonLinePonderationsQuery(celluleId, supervisorUserId, templateId, effectiveAt), ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
    }

    [HttpPut]
    public async Task<ActionResult<List<CommonLinePonderationDto>>> Put(
        string celluleId,
        [FromQuery] string supervisorUserId,
        [FromBody] PutCommonLinePonderationsRequest body,
        CancellationToken ct)
    {
        if (ponderations is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(
                new PutCelluleCommonLinePonderationsCommand(celluleId, supervisorUserId, body), ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
    }

    [HttpPost("consolidate")]
    public async Task<ActionResult<object>> Consolidate(
        string celluleId,
        [FromQuery] string supervisorUserId,
        [FromQuery] string? templateId,
        [FromQuery] DateTimeOffset? effectiveAt,
        CancellationToken ct)
    {
        if (ponderations is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            var closed = await mediator.Send(
                new ConsolidateCelluleCommonLinePonderationsCommand(celluleId, supervisorUserId, templateId, effectiveAt),
                ct);
            return Ok(new { closedServiceOverrides = closed });
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
    }
}
