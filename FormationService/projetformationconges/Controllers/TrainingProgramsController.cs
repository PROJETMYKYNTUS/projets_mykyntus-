using Formation.Application.DTOs;
using Formation.Infrastructure.Services;
using Kyntus.Identity.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Formation.API.Controllers;

[ApiController]
[Route("api/formations/programs")]
public sealed class TrainingProgramsController(TrainingWorkflowService training) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "CanPlanContinue")]
    public async Task<ActionResult<TrainingProgramDto>> Create(
        [FromBody] CreateTrainingProgramRequest body,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(body.CreatedByUserId))
                body.CreatedByUserId = User.GetSubjectId()?.ToString() ?? User.GetEmail() ?? "unknown";
            var created = await training.CreateProgramAsync(body, ct);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "CanPlanContinue")]
    public async Task<ActionResult<TrainingProgramDetailDto>> Get(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await training.GetProgramAsync(id, ct));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/beneficiary-progress")]
    [Authorize(Policy = "CanPlanContinue")]
    public async Task<ActionResult<IReadOnlyList<ProgramBeneficiaryProgressDto>>> BeneficiaryProgress(
        Guid id,
        CancellationToken ct)
    {
        try
        {
            return Ok(await training.GetProgramBeneficiaryProgressAsync(id, ct));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/assign")]
    [Authorize(Policy = "CanPlanContinue")]
    public async Task<ActionResult<IReadOnlyList<TrainingAssignmentDto>>> Assign(
        Guid id,
        [FromBody] AssignTrainingEmployeesRequest body,
        CancellationToken ct)
    {
        try
        {
            return Ok(await training.AssignEmployeesToProgramAsync(id, body, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
