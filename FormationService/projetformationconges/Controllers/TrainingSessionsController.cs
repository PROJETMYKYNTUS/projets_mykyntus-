using Formation.Application.DTOs;
using Formation.Domain.Enums;
using Formation.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Formation.API.Controllers;

[ApiController]
[Route("api/formations/sessions")]
public sealed class TrainingSessionsController(TrainingWorkflowService training) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<TrainingSessionDto>> List(CancellationToken ct) =>
        training.ListSessionsAsync(ct);

    [HttpGet("my-animated")]
    public Task<IReadOnlyList<TrainingSessionDto>> MyAnimated([FromQuery] Guid animatorUserId, CancellationToken ct) =>
        training.ListAnimatedSessionsAsync(animatorUserId, ct);

    [HttpGet("my-assigned")]
    public Task<IReadOnlyList<MyAssignedTrainingSessionDto>> MyAssigned(
        [FromQuery] Guid employeeId,
        CancellationToken ct) =>
        training.ListMyAssignedSessionsAsync(employeeId, ct);

    [HttpPost]
    public async Task<ActionResult<TrainingSessionDto>> Create([FromBody] CreateTrainingSessionRequest body, CancellationToken ct)
    {
        var created = await training.CreateSessionAsync(body, ct);
        return CreatedAtAction(nameof(List), new { id = created.Id }, created);
    }

    [HttpPost("{id:guid}/assign")]
    public Task<IReadOnlyList<TrainingAssignmentDto>> Assign(
        Guid id,
        [FromBody] AssignTrainingEmployeesRequest body,
        CancellationToken ct) =>
        training.AssignEmployeesAsync(id, body, ct);

    [HttpGet("{id:guid}/assignments")]
    public async Task<ActionResult<IReadOnlyList<TrainingAssignmentDto>>> ListAssignments(
        Guid id,
        [FromQuery] Guid animatorUserId,
        CancellationToken ct)
    {
        try
        {
            return Ok(await training.ListSessionAssignmentsAsync(id, animatorUserId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/assignments/{assignmentId:guid}/attendance")]
    public async Task<ActionResult<TrainingAssignmentDto>> MarkAttendance(
        Guid id,
        Guid assignmentId,
        [FromBody] MarkTrainingAttendanceRequest body,
        CancellationToken ct)
    {
        try
        {
            return Ok(await training.MarkAttendanceAsync(id, assignmentId, body, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<TrainingSessionDto>> PatchStatus(
        Guid id,
        [FromBody] PatchTrainingSessionStatusRequest body,
        CancellationToken ct)
    {
        var updated = await training.UpdateSessionStatusAsync(id, body.Status, ct);
        return updated is null ? NotFound() : Ok(updated);
    }
}

public sealed class PatchTrainingSessionStatusRequest
{
    public TrainingSessionStatus Status { get; set; }
}
