using MediatR;
using Microsoft.AspNetCore.Mvc;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Application.Org;

namespace Prime.API.Controllers;

[ApiController]
[Route("api/prime/services/{serviceId}/common-line-ponderations")]
public sealed class ServiceCommonLinePonderationsController(
    IMediator mediator,
    ICommonLinePonderationsAppService? ponderations) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<EffectiveCommonLinePonderationDto>>> Get(
        string serviceId,
        [FromQuery] string supervisorUserId,
        [FromQuery] string? templateId,
        [FromQuery] DateTimeOffset? effectiveAt,
        CancellationToken ct)
    {
        if (ponderations is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(
                new GetServiceCommonLinePonderationsQuery(serviceId, supervisorUserId, templateId, effectiveAt), ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
    }

    [HttpPut]
    public async Task<ActionResult<List<CommonLinePonderationDto>>> Put(
        string serviceId,
        [FromQuery] string supervisorUserId,
        [FromBody] PutCommonLinePonderationsRequest body,
        CancellationToken ct)
    {
        if (ponderations is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(
                new PutServiceCommonLinePonderationsCommand(serviceId, supervisorUserId, body), ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
    }

    [HttpDelete("{templateStableId}")]
    public async Task<IActionResult> Delete(
        string serviceId,
        string templateStableId,
        [FromQuery] string supervisorUserId,
        [FromQuery] string? templateId,
        [FromQuery] DateTimeOffset? effectiveAt,
        CancellationToken ct)
    {
        if (ponderations is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            await mediator.Send(
                new DeleteServiceCommonLinePonderationCommand(
                    serviceId, templateStableId, supervisorUserId, templateId, effectiveAt),
                ct);
            return NoContent();
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
    }
}
