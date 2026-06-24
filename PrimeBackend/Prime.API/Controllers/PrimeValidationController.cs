using MediatR;
using Microsoft.AspNetCore.Mvc;
using Prime.Application;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Application.Fiches;

namespace Prime.API.Controllers;

/// <summary>
/// API de validation des fiches service (Superviseur, Chef de projet).
/// RH / Manager / Comptabilité : <see cref="PrimeGlobalPoolStakeholderController"/>.
/// </summary>
[ApiController]
[Route("api/prime/validation")]
public sealed class PrimeValidationController(IMediator mediator, IPrimeValidationAppService? validation) : ControllerBase
{
    [HttpPost("reconcile-ready")]
    public async Task<ActionResult<object>> ReconcileReady(CancellationToken ct)
    {
        if (validation is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var repair = await mediator.Send(new ReconcileReadyValidationsCommand(), ct);
        return Ok(new
        {
            reconciled = repair.Reconciled,
            draftsValidated = repair.DraftsValidated,
            fichesEnsured = repair.FichesEnsured,
            reconciledGlobal = repair.ReconciledGlobal,
            reconciledByPeriod = repair.ReconciledByPeriod,
        });
    }

    [HttpGet("workflow-meta")]
    public async Task<ActionResult<WorkflowValidationMetaDto>> WorkflowMeta([FromQuery] string? role, CancellationToken ct)
    {
        if (validation is null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await mediator.Send(new GetValidationWorkflowMetaQuery(role), ct));
    }

    [HttpGet]
    public async Task<ActionResult<List<EmployeePrimeServiceFicheValidationDto>>> List(
        [FromQuery] string? period,
        [FromQuery] string? status,
        [FromQuery] string? serviceId,
        [FromQuery] string? celluleId,
        [FromQuery] string? userId,
        [FromQuery] string? role,
        [FromQuery] bool? readyOnly,
        CancellationToken ct)
    {
        if (validation is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(
                new ListValidationsQuery(period, status, serviceId, celluleId, userId, role, readyOnly), ct));
        }
        catch (PrimeApiException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpGet("summary")]
    public async Task<ActionResult<WorkflowValidationSummaryDto>> Summary(
        [FromQuery] string? period,
        [FromQuery] string? serviceId,
        [FromQuery] string? celluleId,
        [FromQuery] string? userId,
        [FromQuery] string? role,
        [FromQuery] bool? readyOnly,
        CancellationToken ct)
    {
        if (validation is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(
                new GetValidationSummaryQuery(period, serviceId, celluleId, userId, role, readyOnly), ct));
        }
        catch (PrimeApiException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpGet("history-feed")]
    public async Task<ActionResult<List<PrimeFicheValidationHistoryFeedItemDto>>> HistoryFeed(
        [FromQuery] string? userId,
        [FromQuery] string? role,
        [FromQuery] string? period,
        [FromQuery] bool? mineOnly,
        [FromQuery] string? action,
        CancellationToken ct)
    {
        if (validation is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(
                new GetValidationHistoryFeedQuery(userId, role, period, mineOnly, action), ct));
        }
        catch (PrimeApiException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpGet("periods")]
    public async Task<ActionResult<List<string>>> Periods(CancellationToken ct)
    {
        if (validation is null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await mediator.Send(new ListValidationPeriodsQuery(), ct));
    }

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<List<PrimeFicheValidationHistoryDto>>> History(
        Guid id,
        [FromQuery] string? userId,
        [FromQuery] string? role,
        CancellationToken ct)
    {
        if (validation is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new GetFicheValidationHistoryQuery(id, userId, role), ct));
        }
        catch (PrimeApiException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<EmployeePrimeServiceFicheValidationDto>> Approve(
        Guid id,
        [FromBody] ApproveServiceFicheRequest body,
        CancellationToken ct)
    {
        if (validation is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new ApproveFicheValidationCommand(id, body), ct));
        }
        catch (PrimeApiException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<EmployeePrimeServiceFicheValidationDto>> Reject(
        Guid id,
        [FromBody] RejectServiceFicheRequest body,
        CancellationToken ct)
    {
        if (validation is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new RejectFicheValidationCommand(id, body), ct));
        }
        catch (PrimeApiException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("bulk-approve")]
    public async Task<ActionResult<object>> BulkApprove(
        [FromBody] BulkApproveServiceFicheRequest body,
        CancellationToken ct)
    {
        if (validation is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            var result = await mediator.Send(new BulkApproveFicheValidationsCommand(body), ct);
            return Ok(new { approvedIds = result.ApprovedIds, ignoredIds = result.IgnoredIds });
        }
        catch (PrimeApiException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/export-csv")]
    public async Task<IActionResult> ExportCsv(
        Guid id,
        [FromQuery] string? userId,
        [FromQuery] string? role,
        CancellationToken ct)
    {
        if (validation is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            var file = await mediator.Send(new ExportFicheCsvQuery(id, userId, role), ct);
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (PrimeApiException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{id:guid}/export-xlsx")]
    public async Task<IActionResult> ExportXlsx(
        Guid id,
        [FromQuery] string? userId,
        [FromQuery] string? role,
        CancellationToken ct)
    {
        if (validation is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            var file = await mediator.Send(new ExportFicheXlsxQuery(id, userId, role), ct);
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (PrimeApiException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
