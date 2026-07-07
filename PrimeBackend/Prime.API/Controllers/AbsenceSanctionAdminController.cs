using MediatR;
using Microsoft.AspNetCore.Mvc;
using Prime.Application;
using Prime.Application.Abstractions;
using Prime.Application.Admin;
using Prime.Application.DTOs;

namespace Prime.API.Controllers;

[ApiController]
[Route("api/prime/admin/absence-sanction-config")]
public sealed class AbsenceSanctionAdminController(
    IMediator mediator,
    IPrimeAbsenceSanctionConfigAppService? configService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PrimeAbsenceSanctionConfigDto>> Get(CancellationToken ct)
    {
        if (configService is null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await mediator.Send(new GetPrimeAbsenceSanctionConfigQuery(), ct));
    }

    [HttpPut]
    public async Task<ActionResult<PrimeAbsenceSanctionConfigDto>> Save(
        [FromBody] SavePrimeAbsenceSanctionConfigRequest body,
        CancellationToken ct)
    {
        if (configService is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new SavePrimeAbsenceSanctionConfigCommand(body), ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (PrimeApiException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }
}
