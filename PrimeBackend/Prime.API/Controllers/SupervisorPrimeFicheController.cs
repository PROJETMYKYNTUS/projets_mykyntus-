using MediatR;
using Microsoft.AspNetCore.Mvc;
using Prime.Application;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Application.Drafts;

namespace Prime.API.Controllers;

[ApiController]
[Route("api/prime/supervisor-fiches")]
public sealed class SupervisorPrimeFicheController(
    IMediator mediator,
    ISupervisorPrimeFicheAppService? fiches) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SupervisorPrimeFicheResponseDto>> Create(
        [FromBody] CreateSupervisorPrimeFicheRequest body,
        CancellationToken ct)
    {
        if (fiches is null) return StatusCode(503, new { error = "Base de données non configurée (connection string)." });
        return Ok(await mediator.Send(new CreateSupervisorPrimeFicheCommand(body), ct));
    }

    [HttpPut("{id:guid}/saisie")]
    public async Task<ActionResult<SupervisorPrimeFicheResponseDto>> UpdateSaisie(
        Guid id,
        [FromBody] UpdateSupervisorPrimeFicheSaisieRequest body,
        CancellationToken ct)
    {
        if (fiches is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new UpdateSupervisorPrimeFicheSaisieCommand(id, body), ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (PrimeApiException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/validate")]
    public async Task<ActionResult<SupervisorPrimeFicheResponseDto>> Validate(Guid id, CancellationToken ct)
    {
        if (fiches is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new ValidateSupervisorPrimeFicheCommand(id), ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<SupervisorPrimeFicheResponseDto>>> List(
        [FromQuery] string supervisorUserId,
        [FromQuery] string? period,
        CancellationToken ct)
    {
        if (fiches is null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await mediator.Send(new ListSupervisorPrimeFichesQuery(supervisorUserId, period), ct));
    }
}
