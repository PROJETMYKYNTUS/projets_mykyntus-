using MediatR;
using Microsoft.AspNetCore.Mvc;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Application.Org;

namespace Prime.API.Controllers;

[ApiController]
[Route("api/prime/services/{serviceId}/pole-line-ponderations")]
public sealed class ServicePoleLinePonderationsController(
    IMediator mediator,
    IServicePoleLinePonderationsAppService? ponderations) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ServicePoleLinePonderationDto>>> Get(
        string serviceId, [FromQuery] string supervisorUserId, CancellationToken ct)
    {
        if (ponderations is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new GetServicePoleLinePonderationsQuery(serviceId, supervisorUserId), ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
    }

    [HttpPut]
    public async Task<ActionResult<List<ServicePoleLinePonderationDto>>> Put(
        string serviceId, [FromQuery] string supervisorUserId,
        [FromBody] PutServicePoleLinePonderationsRequest body, CancellationToken ct)
    {
        if (ponderations is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new PutServicePoleLinePonderationsCommand(serviceId, supervisorUserId, body), ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
    }
}
