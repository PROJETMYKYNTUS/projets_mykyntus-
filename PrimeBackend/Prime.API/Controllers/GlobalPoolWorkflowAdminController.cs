using MediatR;
using Microsoft.AspNetCore.Mvc;
using Prime.Application.Abstractions;
using Prime.Application.Admin;
using Prime.Application.DTOs;

namespace Prime.API.Controllers;

[ApiController]
[Route("api/prime/admin/global-pool-workflow")]
public sealed class GlobalPoolWorkflowAdminController(
    IMediator mediator,
    IGlobalPoolWorkflowAdminService? globalPoolAdmin) : ControllerBase
{
    [HttpGet("steps")]
    public async Task<ActionResult<List<GlobalPoolWorkflowStepDto>>> List(CancellationToken ct)
    {
        if (globalPoolAdmin is null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await mediator.Send(new ListGlobalPoolWorkflowStepsQuery(), ct));
    }

    [HttpPost("steps")]
    public async Task<ActionResult<GlobalPoolWorkflowStepDto>> Create([FromBody] UpsertGlobalPoolWorkflowStepRequest body, CancellationToken ct)
    {
        if (globalPoolAdmin is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new CreateGlobalPoolWorkflowStepCommand(body), ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("steps/{id:guid}")]
    public async Task<ActionResult<GlobalPoolWorkflowStepDto>> Update(Guid id, [FromBody] UpsertGlobalPoolWorkflowStepRequest body, CancellationToken ct)
    {
        if (globalPoolAdmin is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var updated = await mediator.Send(new UpdateGlobalPoolWorkflowStepCommand(id, body), ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("steps/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (globalPoolAdmin is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var deleted = await mediator.Send(new DeleteGlobalPoolWorkflowStepCommand(id), ct);
        return deleted ? NoContent() : NotFound();
    }
}
