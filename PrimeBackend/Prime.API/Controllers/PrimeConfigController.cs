using MediatR;
using Microsoft.AspNetCore.Mvc;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Application.LegacyRead;
using Prime.Domain.Entities;

namespace Prime.API.Controllers;

[ApiController]
[Route("api/prime/config")]
public sealed class PrimeConfigController(IMediator mediator, IPrimeAdminReadAppService? admin) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PrimeConfigItem>>> GetConfigs(
        [FromQuery] string? kind,
        [FromQuery] string? sector,
        [FromQuery] string? groupCode,
        [FromQuery] string? activityType,
        CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await mediator.Send(new GetPrimeConfigsQuery(kind, sector, groupCode, activityType), ct));
    }

    [HttpPost]
    public async Task<ActionResult<PrimeConfigItem>> CreateConfig([FromBody] PrimeConfigUpsertRequest req, CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await mediator.Send(new CreatePrimeConfigCommand(req), ct));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<PrimeConfigItem>> UpdateConfig(string id, [FromBody] PrimeConfigUpsertRequest req, CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await mediator.Send(new UpdatePrimeConfigCommand(id, req), ct));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteConfig(string id, CancellationToken ct)
    {
        if (admin is null) return StatusCode(503, new { error = "Base de données non configurée." });
        await mediator.Send(new DeletePrimeConfigCommand(id), ct);
        return NoContent();
    }
}
