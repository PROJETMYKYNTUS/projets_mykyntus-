using MediatR;
using Microsoft.AspNetCore.Mvc;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Application.Org;

namespace Prime.API.Controllers;

[ApiController]
[Route("api/prime/fiche-imports")]
public sealed class PrimeFicheImportController(IMediator mediator, IPrimeFicheImportAppService? import) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ImportReadyFicheResponseDto>> Import(
        [FromBody] ImportReadyFicheRequest body, CancellationToken ct)
    {
        if (import is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            return Ok(await mediator.Send(new ImportPrimeFicheCommand(body), ct));
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("existe déjà", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { error = ex.Message });
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("historical")]
    public async Task<ActionResult<List<PrimeHistoricalFicheListItemDto>>> ListHistorical(
        [FromQuery] string supervisorUserId, [FromQuery] string? period, [FromQuery] string? role, CancellationToken ct)
    {
        if (import is null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(supervisorUserId))
            return BadRequest(new { error = "supervisorUserId est requis." });
        return Ok(await mediator.Send(new ListHistoricalPrimeFichesQuery(supervisorUserId, period, role), ct));
    }

    [HttpGet("historical/{id:guid}/detail-snapshot")]
    public async Task<ActionResult<PrimeHistoricalFicheDetailSnapshotDto>> GetHistoricalDetailSnapshot(
        Guid id, [FromQuery] string supervisorUserId, [FromQuery] string? role, CancellationToken ct)
    {
        if (import is null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(supervisorUserId))
            return BadRequest(new { error = "supervisorUserId est requis." });
        try
        {
            return Ok(await mediator.Send(new GetHistoricalPrimeFicheDetailQuery(id, supervisorUserId, role), ct));
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("introuvable", StringComparison.OrdinalIgnoreCase))
                return NotFound(new { error = ex.Message });
            if (ex.Message.Contains("refusé", StringComparison.OrdinalIgnoreCase))
                return StatusCode(403, new { error = ex.Message });
            return BadRequest(new { error = ex.Message });
        }
    }
}
