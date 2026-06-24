using MediatR;
using Microsoft.AspNetCore.Mvc;
using Prime.Application;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Application.Org;

namespace Prime.API.Controllers;

[ApiController]
[Route("api/prime/services/{serviceId}/prime-indicators")]
public sealed class ServicePrimeIndicatorsController(
    IMediator mediator,
    IServicePrimeIndicatorsAppService? indicators) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ServicePrimeIndicatorDto>>> Get(
        string serviceId, [FromQuery] string supervisorUserId, CancellationToken ct)
    {
        if (indicators is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new GetServicePrimeIndicatorsQuery(serviceId, supervisorUserId), ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
    }

    [HttpPut]
    public async Task<ActionResult<List<ServicePrimeIndicatorDto>>> Put(
        string serviceId, [FromQuery] string supervisorUserId,
        [FromBody] PutServicePrimeIndicatorsRequest body, CancellationToken ct)
    {
        if (indicators is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new PutServicePrimeIndicatorsCommand(serviceId, supervisorUserId, body), ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
    }
}
