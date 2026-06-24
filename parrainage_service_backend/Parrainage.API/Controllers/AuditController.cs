using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parrainage.Application.Audit;
using Parrainage.Application.DTOs;

namespace Parrainage.API.Controllers;

[ApiController]
[Route("api/parrainage/audit")]
public sealed class AuditController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AuditLogDto>>> List([FromQuery] int? take, CancellationToken ct) =>
        Ok(await mediator.Send(new ListParrainageAuditLogsQuery(take), ct));

    [HttpPost]
    public async Task<ActionResult<AuditLogDto>> Create([FromBody] CreateAuditRequest body, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.Send(new CreateParrainageAuditLogCommand(body), ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
