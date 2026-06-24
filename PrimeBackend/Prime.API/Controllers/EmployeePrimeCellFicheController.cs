using MediatR;
using Microsoft.AspNetCore.Mvc;
using Prime.Application;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Application.Fiches;

namespace Prime.API.Controllers;

[ApiController]
[Route("api/prime/employee-prime-service-fiches")]
[Route("api/prime/employee-prime-cell-fiches")]
public sealed class EmployeePrimeServiceFicheController(
    IMediator mediator,
    IEmployeePrimeServiceFicheAppService? fiches) : ControllerBase
{
    [HttpGet("list")]
    public async Task<ActionResult<List<EmployeePrimeServiceFicheListItemDto>>> List(
        [FromQuery] string? serviceId,
        [FromQuery] string? celluleId,
        [FromQuery] string period,
        [FromQuery] string supervisorUserId,
        CancellationToken ct)
    {
        if (fiches is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            var result = await mediator.Send(
                new ListEmployeePrimeServiceFichesQuery(serviceId, celluleId, period, supervisorUserId), ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }

    [HttpGet("for-employee")]
    public async Task<ActionResult<EmployeePrimeServiceFicheResponseDto>> GetForEmployee(
        [FromQuery] string supervisorUserId,
        [FromQuery] string employeeId,
        [FromQuery] string period,
        [FromQuery] string? templateId,
        CancellationToken ct)
    {
        if (fiches is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(
                new GetEmployeePrimeServiceFicheForEmployeeQuery(supervisorUserId, employeeId, period, templateId), ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }

    [HttpPut]
    public async Task<ActionResult<EmployeePrimeServiceFicheResponseDto>> Upsert(
        [FromBody] UpsertEmployeePrimeServiceFicheRequest body,
        CancellationToken ct)
    {
        if (fiches is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new UpsertEmployeePrimeServiceFicheCommand(body), ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }

    [HttpPost("{ficheId:guid}/amounts")]
    public async Task<ActionResult<EmployeePrimeServiceFicheResponseDto>> PersistAmounts(
        Guid ficheId,
        [FromBody] PersistFicheAmountsRequest body,
        CancellationToken ct)
    {
        if (fiches is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new PersistEmployeePrimeServiceFicheAmountsCommand(ficheId, body), ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }
}
