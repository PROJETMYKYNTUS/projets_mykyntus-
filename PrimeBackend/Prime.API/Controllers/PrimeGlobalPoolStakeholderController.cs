using MediatR;
using Microsoft.AspNetCore.Mvc;
using Prime.Application;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Application.GlobalPool;

namespace Prime.API.Controllers;

/// <summary>Synthèse globale PRIME — workflow pool configurable ou colonnes historiques.</summary>
[ApiController]
[Route("api/prime/global-pool")]
public sealed class PrimeGlobalPoolStakeholderController(
    IMediator mediator,
    IPrimeGlobalPoolStakeholderAppService? pool) : ControllerBase
{
    [HttpGet("inbox")]
    public async Task<ActionResult<List<GlobalPoolInboxItemDto>>> Inbox(
        [FromQuery] string userId,
        [FromQuery] string? role,
        CancellationToken ct)
    {
        if (pool is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new GetGlobalPoolInboxQuery(userId, role), ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (PrimeApiException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }

    [HttpGet("{draftId:guid}/excel")]
    public async Task<IActionResult> DownloadExcel(Guid draftId, [FromQuery] string userId, CancellationToken ct)
    {
        if (pool is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            var file = await mediator.Send(new DownloadGlobalPoolDraftExcelQuery(draftId, userId), ct);
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (PrimeApiException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }

    [HttpPost("{draftId:guid}/approve-step")]
    public async Task<IActionResult> ApproveStep(
        Guid draftId, [FromBody] GlobalPoolApproveStepRequest body, CancellationToken ct)
    {
        if (pool is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new ApproveGlobalPoolDraftStepCommand(draftId, body), ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (PrimeApiException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }

    [HttpPost("{draftId:guid}/approve-manager")]
    public async Task<IActionResult> ApproveManagerStakeholder(
        Guid draftId, [FromBody] GlobalPoolActingUserRequest body, CancellationToken ct)
    {
        if (pool is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new ApproveGlobalPoolDraftManagerCommand(draftId, body), ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (PrimeApiException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }

    [HttpPost("{draftId:guid}/approve-rh")]
    public async Task<IActionResult> ApproveRhStakeholder(
        Guid draftId, [FromBody] GlobalPoolActingUserRequest body, CancellationToken ct)
    {
        if (pool is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new ApproveGlobalPoolDraftRhCommand(draftId, body), ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (PrimeApiException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }

    [HttpPost("{draftId:guid}/ack-compta")]
    public async Task<IActionResult> AckComptaStakeholder(
        Guid draftId, [FromBody] GlobalPoolActingUserRequest body, CancellationToken ct)
    {
        if (pool is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new AckGlobalPoolDraftComptaCommand(draftId, body), ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (PrimeApiException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }
}
