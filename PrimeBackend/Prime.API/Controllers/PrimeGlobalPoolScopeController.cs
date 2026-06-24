using MediatR;
using Microsoft.AspNetCore.Mvc;
using Prime.Application;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Application.GlobalPool;

namespace Prime.API.Controllers;

/// <summary>Synthèse globale PRIME par périmètre (service / cellule / pôle).</summary>
[ApiController]
[Route("api/prime/global-pool")]
public sealed class PrimeGlobalPoolScopeController(IMediator mediator, IPrimeGlobalPoolScopeAppService? scope) : ControllerBase
{
    [HttpGet("readiness")]
    public async Task<ActionResult<GlobalPoolReadinessDto>> Readiness([FromQuery] string period, CancellationToken ct)
    {
        if (scope is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try { return Ok(await mediator.Send(new GetGlobalPoolReadinessQuery(period), ct)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("synthesis/lines")]
    public async Task<ActionResult<GlobalSynthesisLinesResponseDto>> SynthesisLines(
        [FromQuery] string period, [FromQuery] string scopeType, [FromQuery] string scopeId,
        [FromQuery] Guid? scopeSynthesisId, [FromQuery] string? userId, CancellationToken ct)
    {
        if (scope is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(
                new GetGlobalSynthesisLinesQuery(period, scopeType, scopeId, scopeSynthesisId, userId), ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("synthesis/summary")]
    public async Task<ActionResult<GlobalSynthesisSummaryDto>> SynthesisSummary(
        [FromQuery] string period, [FromQuery] string scopeType, [FromQuery] string scopeId,
        [FromQuery] Guid? scopeSynthesisId, CancellationToken ct)
    {
        if (scope is null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await mediator.Send(
            new GetGlobalSynthesisSummaryQuery(period, scopeType, scopeId, scopeSynthesisId), ct));
    }

    [HttpPost("synthesis/generate")]
    public async Task<IActionResult> GenerateSynthesis([FromBody] GenerateScopeSynthesisRequest body, CancellationToken ct)
    {
        if (scope is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            var r = await mediator.Send(new GenerateGlobalSynthesisCommand(body), ct);
            return Ok(new { scopeSynthesisId = r.ScopeSynthesisId, fileName = r.FileName, generatedAt = r.GeneratedAt });
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (PrimeApiException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }

    [HttpPost("synthesis/ensure")]
    public async Task<IActionResult> EnsureSynthesis([FromBody] GenerateScopeSynthesisRequest body, CancellationToken ct)
    {
        if (scope is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            var r = await mediator.Send(new EnsureGlobalSynthesisCommand(body), ct);
            return Ok(new
            {
                scopeSynthesisId = r.ScopeSynthesisId,
                ready = r.Ready,
                fileName = r.FileName,
                generatedAt = r.GeneratedAt,
                error = r.Error,
            });
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (PrimeApiException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }

    [HttpGet("scope-inbox")]
    public async Task<ActionResult<List<GlobalPoolScopeSynthesisInboxItemDto>>> ScopeInbox(
        [FromQuery] string userId, [FromQuery] string? role, CancellationToken ct)
    {
        if (scope is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try { return Ok(await mediator.Send(new GetGlobalPoolScopeInboxQuery(userId, role), ct)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (PrimeApiException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }

    [HttpGet("scope-synthesis/{scopeSynthesisId:guid}/excel")]
    public async Task<IActionResult> DownloadScopeExcel(Guid scopeSynthesisId, [FromQuery] string userId, CancellationToken ct)
    {
        if (scope is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            var file = await mediator.Send(new DownloadGlobalPoolScopeExcelQuery(scopeSynthesisId, userId), ct);
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (PrimeApiException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }

    [HttpPost("scope-synthesis/{scopeSynthesisId:guid}/approve-step")]
    public async Task<IActionResult> ApproveScopeStep(
        Guid scopeSynthesisId, [FromBody] GlobalPoolApproveStepRequest body, CancellationToken ct)
    {
        if (scope is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new ApproveGlobalPoolScopeStepCommand(scopeSynthesisId, body), ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (PrimeApiException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }

    [HttpPost("scope-synthesis/{scopeSynthesisId:guid}/approve-manager")]
    public async Task<IActionResult> ApproveScopeManager(
        Guid scopeSynthesisId, [FromBody] GlobalPoolActingUserRequest body, CancellationToken ct)
    {
        if (scope is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new ApproveGlobalPoolScopeManagerCommand(scopeSynthesisId, body), ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (PrimeApiException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }

    [HttpPost("scope-synthesis/{scopeSynthesisId:guid}/approve-rh")]
    public async Task<IActionResult> ApproveScopeRh(
        Guid scopeSynthesisId, [FromBody] GlobalPoolActingUserRequest body, CancellationToken ct)
    {
        if (scope is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new ApproveGlobalPoolScopeRhCommand(scopeSynthesisId, body), ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (PrimeApiException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }

    [HttpPost("scope-synthesis/{scopeSynthesisId:guid}/ack-compta")]
    public async Task<IActionResult> AckScopeCompta(
        Guid scopeSynthesisId, [FromBody] GlobalPoolActingUserRequest body, CancellationToken ct)
    {
        if (scope is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new AckGlobalPoolScopeComptaCommand(scopeSynthesisId, body), ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (PrimeApiException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }

    [HttpPost("synthesis/lines/{lineId:guid}/reject")]
    public async Task<IActionResult> RejectLine(Guid lineId, [FromBody] RejectSynthesisLineRequest body, CancellationToken ct)
    {
        if (scope is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            await mediator.Send(new RejectGlobalSynthesisLineCommand(lineId, body), ct);
            return Ok(new { ok = true });
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (PrimeApiException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }

    [HttpPost("synthesis/lines/{lineId:guid}/approve")]
    public async Task<IActionResult> ApproveLine(Guid lineId, [FromBody] GlobalPoolActingUserRequest body, CancellationToken ct)
    {
        if (scope is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            await mediator.Send(new ApproveGlobalSynthesisLineCommand(lineId, body), ct);
            return Ok(new { ok = true });
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (PrimeApiException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }

    [HttpGet("supervisor-synthesis-tracking")]
    public async Task<ActionResult<List<SupervisorSynthesisTrackingItemDto>>> SupervisorSynthesisTracking(
        [FromQuery] string supervisorUserId, [FromQuery] string period, CancellationToken ct)
    {
        if (scope is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new GetSupervisorSynthesisTrackingQuery(supervisorUserId, period), ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("my-synthesis-tracking")]
    public async Task<ActionResult<List<EmployeePrimePaymentTrackingDto>>> MySynthesisTracking(
        [FromQuery] string? userId, [FromQuery] string? role, CancellationToken ct)
    {
        if (scope is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try { return Ok(await mediator.Send(new GetMySynthesisTrackingQuery(userId, role), ct)); }
        catch (PrimeApiException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }

    [HttpGet("synthesis-tracking-feed")]
    public async Task<ActionResult<List<PrimeFicheValidationHistoryFeedItemDto>>> SynthesisTrackingFeed(
        [FromQuery] string? userId, [FromQuery] string? role, [FromQuery] string? period,
        [FromQuery] bool? mineOnly, [FromQuery] string? action, CancellationToken ct)
    {
        if (scope is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(
                new GetSynthesisTrackingFeedQuery(userId, role, period, mineOnly, action), ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (PrimeApiException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }

    [HttpGet("synthesis/lines/{lineId:guid}/history")]
    public async Task<ActionResult<List<GlobalPoolSynthesisLineHistoryDto>>> SynthesisLineHistory(
        Guid lineId, [FromQuery] string? userId, [FromQuery] string? role, CancellationToken ct)
    {
        if (scope is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new GetSynthesisLineHistoryQuery(lineId, userId, role), ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (PrimeApiException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }

    [HttpPost("synthesis/lines/{lineId:guid}/payment")]
    public async Task<IActionResult> SetLinePayment(Guid lineId, [FromBody] SetSynthesisLinePaymentRequest body, CancellationToken ct)
    {
        if (scope is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            await mediator.Send(new SetGlobalSynthesisLinePaymentCommand(lineId, body), ct);
            return Ok(new { ok = true });
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (PrimeApiException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }

    [HttpPost("scope-synthesis/{scopeSynthesisId:guid}/pay-all")]
    public async Task<IActionResult> PayAll(Guid scopeSynthesisId, [FromBody] PaySynthesisAllRequest body, CancellationToken ct)
    {
        if (scope is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            await mediator.Send(new PayAllGlobalSynthesisCommand(scopeSynthesisId, body), ct);
            return Ok(new { ok = true });
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (PrimeApiException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }

    [HttpGet("periods")]
    public async Task<ActionResult<List<string>>> ListPeriods(CancellationToken ct)
    {
        if (scope is null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await mediator.Send(new ListGlobalPoolPeriodsQuery(), ct));
    }
}
