using MediatR;
using Microsoft.AspNetCore.Mvc;
using Prime.Application.Abstractions;
using Prime.Application.Admin;
using Prime.Application.DTOs;

namespace Prime.API.Controllers;

/// <summary>API de configuration du workflow de validation (Phase 1.4).</summary>
[ApiController]
[Route("api/prime/admin/workflow")]
public sealed class WorkflowConfigController(IMediator mediator, IWorkflowConfigAdminService? workflowAdmin) : ControllerBase
{
    [HttpGet("steps")]
    public async Task<ActionResult<List<WorkflowStepConfigDto>>> ListSteps(CancellationToken ct)
    {
        if (workflowAdmin is null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await mediator.Send(new ListWorkflowStepsQuery(), ct));
    }

    [HttpPost("steps")]
    public async Task<ActionResult<WorkflowStepConfigDto>> CreateStep([FromBody] UpsertWorkflowStepConfigRequest body, CancellationToken ct)
    {
        if (workflowAdmin is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new CreateWorkflowStepCommand(body), ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("steps/{id:guid}")]
    public async Task<ActionResult<WorkflowStepConfigDto>> UpdateStep(Guid id, [FromBody] UpsertWorkflowStepConfigRequest body, CancellationToken ct)
    {
        if (workflowAdmin is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var updated = await mediator.Send(new UpdateWorkflowStepCommand(id, body), ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("steps/rechain")]
    public async Task<ActionResult<List<WorkflowStepConfigDto>>> RechainAllSteps(CancellationToken ct)
    {
        if (workflowAdmin is null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await mediator.Send(new RechainWorkflowStepsCommand(), ct));
    }

    [HttpDelete("steps/{id:guid}")]
    public async Task<IActionResult> DeleteStep(Guid id, CancellationToken ct)
    {
        if (workflowAdmin is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var deleted = await mediator.Send(new DeleteWorkflowStepCommand(id), ct);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("global")]
    public async Task<ActionResult<WorkflowGlobalConfigDto>> GetGlobal(CancellationToken ct)
    {
        if (workflowAdmin is null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await mediator.Send(new GetWorkflowGlobalConfigQuery(), ct));
    }

    [HttpPut("global")]
    public async Task<ActionResult<WorkflowGlobalConfigDto>> UpdateGlobal([FromBody] UpdateWorkflowGlobalConfigRequest body, CancellationToken ct)
    {
        if (workflowAdmin is null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await mediator.Send(new UpdateWorkflowGlobalConfigCommand(body), ct));
    }
}
