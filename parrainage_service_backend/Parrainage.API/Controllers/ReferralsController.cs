using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parrainage.Application.Abstractions;
using Parrainage.Application.Authorization;
using Parrainage.Application.DTOs;
using Parrainage.Application.Referrals;

namespace Parrainage.API.Controllers;

[ApiController]
[Route("api/parrainage/referrals")]
public sealed class ReferralsController(
    IMediator mediator,
    IParrainageRequestUserResolver userResolver) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ReferralDto>>> List(CancellationToken ct) =>
        Ok(await mediator.Send(new ListReferralsQuery(), ct));

    [HttpGet("history")]
    public async Task<ActionResult<List<ReferralHistoryDto>>> History(CancellationToken ct) =>
        Ok(await mediator.Send(new GetReferralHistoryQuery(), ct));

    [HttpGet("onboarding")]
    public async Task<ActionResult<List<ReferralDto>>> Onboarding(CancellationToken ct) =>
        Ok(await mediator.Send(new ListOnboardingReferralsQuery(), ct));

    [HttpGet("{id}")]
    public async Task<ActionResult<ReferralDto>> GetById(string id, CancellationToken ct)
    {
        var dto = await mediator.Send(new GetReferralByIdQuery(id), ct);
        if (dto is null)
            return NotFound(new { error = $"Parrainage introuvable : {id}" });
        return Ok(dto);
    }

    [HttpGet("{id}/reward-preview")]
    public async Task<ActionResult<ReferralRewardPreviewDto>> RewardPreview(string id, CancellationToken ct)
    {
        var dto = await mediator.Send(new GetReferralRewardPreviewQuery(id), ct);
        if (dto is null)
            return NotFound(new { error = $"Parrainage introuvable : {id}" });
        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<ReferralDto>> Create([FromBody] CreateReferralRequest body, CancellationToken ct)
    {
        try
        {
            var created = await mediator.Send(new CreateReferralCommand(body), ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<ReferralDto>> Update(string id, [FromBody] UpdateReferralRequest body, CancellationToken ct)
    {
        try
        {
            var updated = await mediator.Send(new UpdateReferralCommand(id, body), ct);
            if (updated is null)
                return NotFound(new { error = $"Parrainage introuvable : {id}" });
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/process")]
    public Task<ActionResult<ReferralDto>> Process(string id, [FromBody] ProcessReferralRequest body, CancellationToken ct) =>
        ExecuteRhWorkflow(id, body, (actorBody) => new ProcessReferralCommand(id, actorBody), ct);

    [HttpPost("{id}/approve")]
    public Task<ActionResult<ReferralDto>> Approve(string id, [FromBody] ApproveReferralRequest body, CancellationToken ct) =>
        ExecuteRhWorkflow(id, body, (actorBody) => new ApproveReferralCommand(id, actorBody), ct);

    [HttpPost("{id}/confirm-production")]
    public Task<ActionResult<ReferralDto>> ConfirmProduction(string id, [FromBody] ConfirmProductionStartRequest body, CancellationToken ct) =>
        ExecuteRhWorkflow(id, body, (actorBody) => new ConfirmProductionCommand(id, actorBody), ct);

    [HttpPost("{id}/reject-early-departure")]
    public Task<ActionResult<ReferralDto>> RejectEarlyDeparture(string id, [FromBody] RejectEarlyDepartureRequest body, CancellationToken ct) =>
        ExecuteRhWorkflow(id, body, (actorBody) => new RejectEarlyDepartureCommand(id, actorBody), ct);

    [HttpPost("{id}/extend-training")]
    public Task<ActionResult<ReferralDto>> ExtendTraining(string id, [FromBody] ExtendTrainingRequest body, CancellationToken ct) =>
        ExecuteRhWorkflow(id, body, (actorBody) => new ExtendTrainingCommand(id, actorBody), ct);

    [HttpPost("{id}/confirm-eligibility")]
    public Task<ActionResult<ReferralDto>> ConfirmEligibility(string id, [FromBody] ConfirmPaymentEligibilityRequest body, CancellationToken ct) =>
        ExecuteRhWorkflow(id, body, (actorBody) => new ConfirmEligibilityCommand(id, actorBody), ct);

    [HttpPost("{id}/link-employee")]
    public Task<ActionResult<ReferralDto>> LinkEmployee(string id, [FromBody] LinkEmployeeRequest body, CancellationToken ct) =>
        ExecuteRhWorkflow(id, body, (actorBody) => new LinkEmployeeCommand(id, actorBody), ct);

    [HttpPost("{id}/complete-onboarding")]
    public Task<ActionResult<ReferralDto>> CompleteOnboarding(string id, [FromBody] CompleteOnboardingRequest body, CancellationToken ct) =>
        ExecuteRhWorkflow(id, body, (actorBody) => new CompleteOnboardingCommand(id, actorBody), ct);

    [HttpPost("{id}/status")]
    public async Task<ActionResult<ReferralDto>> ChangeStatus(string id, [FromBody] UpdateStatusRequest body, CancellationToken ct)
    {
        try
        {
            var updated = await mediator.Send(new ChangeReferralStatusCommand(id, body), ct);
            if (updated is null)
                return NotFound(new { error = $"Parrainage introuvable : {id}" });
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/reward")]
    public async Task<ActionResult<ReferralDto>> Reward(string id, [FromBody] RewardRequest body, CancellationToken ct)
    {
        var user = userResolver.Resolve(null, null, null);
        if (!ParrainageRoleGuard.CanMarkPayment(user.Role))
            return Forbid();

        try
        {
            var updated = await mediator.Send(new RewardReferralCommand(id, body, user), ct);
            if (updated is null)
                return NotFound(new { error = $"Parrainage introuvable : {id}" });
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/payment")]
    public async Task<ActionResult<ReferralDto>> Payment(string id, [FromBody] MarkReferralPaymentRequest body, CancellationToken ct)
    {
        var user = userResolver.Resolve(null, null, null);
        if (!ParrainageRoleGuard.CanMarkPayment(user.Role))
            return Forbid();

        body.Actor ??= new ActorDto { Id = user.UserId, Label = user.Role, Role = user.Role };

        try
        {
            var updated = await mediator.Send(new MarkReferralPaymentCommand(id, body), ct);
            if (updated is null)
                return NotFound(new { error = $"Parrainage introuvable : {id}" });
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/cv")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ReferralDto>> UploadCv(string id, IFormFile file, CancellationToken ct)
    {
        try
        {
            var updated = await mediator.Send(new UploadReferralCvCommand(id, file), ct);
            if (updated is null)
                return NotFound(new { error = $"Parrainage introuvable : {id}" });
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id}/cv")]
    public async Task<IActionResult> DownloadCv(string id, [FromQuery] string? disposition, CancellationToken ct)
    {
        var file = await mediator.Send(new OpenReferralCvQuery(id), ct);
        if (file is null)
            return NotFound(new { error = "CV introuvable pour ce parrainage." });

        var inline = string.Equals(disposition, "inline", StringComparison.OrdinalIgnoreCase);
        if (inline)
        {
            Response.Headers.ContentDisposition = $"inline; filename=\"{file.FileName}\"";
            return File(file.Stream, file.ContentType, enableRangeProcessing: true);
        }

        return File(file.Stream, file.ContentType, file.FileName, enableRangeProcessing: true);
    }

    private async Task<ActionResult<ReferralDto>> ExecuteRhWorkflow<TBody>(
        string id,
        TBody body,
        Func<TBody, IRequest<ReferralDto?>> buildCommand,
        CancellationToken ct)
        where TBody : class
    {
        var user = userResolver.Resolve(null, null, null);
        if (!ParrainageRoleGuard.IsRh(user.Role))
            return Forbid();

        ApplyActor(body, user);

        try
        {
            var updated = await mediator.Send(buildCommand(body), ct);
            if (updated is null)
                return NotFound(new { error = $"Parrainage introuvable : {id}" });
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static void ApplyActor<TBody>(TBody body, ParrainageResolvedUser user)
    {
        switch (body)
        {
            case ProcessReferralRequest process when process.Actor is null:
                process.Actor = new ActorDto { Id = user.UserId, Label = user.Role, Role = user.Role };
                break;
            case ApproveReferralRequest approve when approve.Actor is null:
                approve.Actor = new ActorDto { Id = user.UserId, Label = user.Role, Role = user.Role };
                break;
            case ConfirmProductionStartRequest confirm when confirm.Actor is null:
                confirm.Actor = new ActorDto { Id = user.UserId, Label = user.Role, Role = user.Role };
                break;
            case RejectEarlyDepartureRequest reject when reject.Actor is null:
                reject.Actor = new ActorDto { Id = user.UserId, Label = user.Role, Role = user.Role };
                break;
            case ExtendTrainingRequest extend when extend.Actor is null:
                extend.Actor = new ActorDto { Id = user.UserId, Label = user.Role, Role = user.Role };
                break;
            case ConfirmPaymentEligibilityRequest eligibility when eligibility.Actor is null:
                eligibility.Actor = new ActorDto { Id = user.UserId, Label = user.Role, Role = user.Role };
                break;
            case LinkEmployeeRequest link when link.Actor is null:
                link.Actor = new ActorDto { Id = user.UserId, Label = user.Role, Role = user.Role };
                break;
            case CompleteOnboardingRequest onboarding when onboarding.Actor is null:
                onboarding.Actor = new ActorDto { Id = user.UserId, Label = user.Role, Role = user.Role };
                break;
        }
    }
}
