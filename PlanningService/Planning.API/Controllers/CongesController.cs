using MediatR;
using Microsoft.AspNetCore.Mvc;
using Planning.Application.Conges;
using Planning.Application.DTOs;

namespace Planning.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CongesController(IMediator mediator) : ControllerBase
{
    [HttpGet("subservice/{subServiceId}")]
    public async Task<IActionResult> GetBySubService(
        int subServiceId,
        [FromQuery] string? weekStart = null,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetCongesBySubServiceQuery(subServiceId, weekStart), ct);
        return Ok(result);
    }

    [HttpGet("new-employees/{subServiceId}")]
    public async Task<IActionResult> GetNewEmployees(int subServiceId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetNewEmployeesBySubServiceQuery(subServiceId), ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCongeDto dto, CancellationToken ct)
    {
        try
        {
            var result = await mediator.Send(new CreatePlanningCongeCommand(dto), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!await mediator.Send(new DeletePlanningCongeCommand(id), ct))
            return NotFound();
        return NoContent();
    }

    [HttpPost("saturday-slot")]
    public async Task<IActionResult> SetSaturdaySlot([FromBody] SetSaturdaySlotDto dto, CancellationToken ct)
    {
        try
        {
            var result = await mediator.Send(new SetSaturdaySlotCommand(dto), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("absence-days/bulk")]
    public async Task<IActionResult> GetBulkAbsenceDays(
        [FromBody] BulkAbsenceDaysRequestDto dto,
        CancellationToken ct = default)
    {
        try
        {
            var result = await mediator.Send(new GetBulkAbsenceDaysCommand(dto), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
