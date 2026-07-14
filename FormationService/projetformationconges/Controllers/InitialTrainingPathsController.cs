using Formation.Application.DTOs;
using Formation.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Formation.API.Controllers;

[ApiController]
[Route("api/formations/initial-paths")]
public sealed class InitialTrainingPathsController(TrainingWorkflowService training) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<InitialTrainingPathDto>> Create([FromBody] CreateInitialTrainingPathRequest body, CancellationToken ct) =>
        Ok(await training.CreateInitialPathAsync(body, ct));

    [HttpGet("formateur")]
    public Task<IReadOnlyList<InitialTrainingPathDto>> FormateurQueue(CancellationToken ct) =>
        training.ListInitialForFormateurAsync(ct);

    [HttpGet("rh-pending")]
    public Task<IReadOnlyList<InitialTrainingPathDto>> RhPending(CancellationToken ct) =>
        training.ListInitialPendingRhAsync(ct);

    [HttpGet("overview")]
    public Task<IReadOnlyList<InitialTrainingPathDto>> Overview(CancellationToken ct) =>
        training.ListInitialOverviewAsync(ct);

    [HttpGet("by-employee/{employeeId:guid}")]
    public Task<IReadOnlyList<InitialTrainingPathDto>> ByEmployee(Guid employeeId, CancellationToken ct) =>
        training.ListInitialByEmployeeAsync(employeeId, ct);

    [HttpPost("{id:guid}/quiz-result")]
    public async Task<ActionResult<InitialTrainingPathDto>> QuizResult(
        Guid id,
        [FromBody] RecordInitialQuizRequest body,
        CancellationToken ct)
    {
        var updated = await training.RecordQuizAsync(id, body, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("{id:guid}/formateur-validate")]
    public async Task<ActionResult<InitialTrainingPathDto>> FormateurValidate(Guid id, CancellationToken ct)
    {
        var updated = await training.FormateurValidateAsync(id, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("{id:guid}/formateur-reject")]
    public async Task<ActionResult<InitialTrainingPathDto>> FormateurReject(
        Guid id,
        [FromBody] RejectInitialTrainingRequest body,
        CancellationToken ct)
    {
        var updated = await training.FormateurRejectAsync(id, body, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("{id:guid}/extend")]
    public async Task<ActionResult<InitialTrainingPathDto>> Extend(
        Guid id,
        [FromBody] ExtendInitialTrainingRequest body,
        CancellationToken ct)
    {
        var updated = await training.ExtendInitialAsync(id, body, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("{id:guid}/rh-validate")]
    public async Task<ActionResult<InitialTrainingPathDto>> RhValidate(Guid id, CancellationToken ct)
    {
        var updated = await training.RhValidateAsync(id, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("{id:guid}/rh-reject")]
    public async Task<ActionResult<InitialTrainingPathDto>> RhReject(
        Guid id,
        [FromBody] RejectInitialTrainingRequest body,
        CancellationToken ct)
    {
        var updated = await training.RhRejectAsync(id, body, ct);
        return updated is null ? NotFound() : Ok(updated);
    }
}
