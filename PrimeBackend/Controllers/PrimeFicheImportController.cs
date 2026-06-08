using Microsoft.AspNetCore.Mvc;
using PrimeBackend.Dto;
using PrimeBackend.Services;

namespace PrimeBackend.Controllers;

[ApiController]
[Route("api/prime/fiche-imports")]
public sealed class PrimeFicheImportController(PrimeFicheImportService? importService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ImportReadyFicheResponseDto>> Import(
        [FromBody] ImportReadyFicheRequest body,
        CancellationToken ct)
    {
        if (importService is null)
            return StatusCode(503, new { error = "Base de données non configurée." });

        var (ok, err, result) = await importService.ImportReadyFicheAsync(body, ct);
        if (!ok || result is null)
        {
            if (err?.Contains("existe déjà", StringComparison.OrdinalIgnoreCase) == true)
                return Conflict(new { error = err });
            return BadRequest(new { error = err ?? "Import impossible." });
        }
        return Ok(result);
    }

    [HttpGet("historical")]
    public async Task<ActionResult<List<PrimeHistoricalFicheListItemDto>>> ListHistorical(
        [FromQuery] string supervisorUserId,
        [FromQuery] string? period,
        [FromQuery] string? role,
        CancellationToken ct)
    {
        if (importService is null)
            return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(supervisorUserId))
            return BadRequest(new { error = "supervisorUserId est requis." });

        return Ok(await importService.ListHistoricalAsync(supervisorUserId, period, role, ct));
    }

    [HttpGet("historical/{id:guid}/detail-snapshot")]
    public async Task<ActionResult<PrimeHistoricalFicheDetailSnapshotDto>> GetHistoricalDetailSnapshot(
        Guid id,
        [FromQuery] string supervisorUserId,
        [FromQuery] string? role,
        CancellationToken ct)
    {
        if (importService is null)
            return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(supervisorUserId))
            return BadRequest(new { error = "supervisorUserId est requis." });

        var (ok, err, result) = await importService.GetHistoricalDetailSnapshotAsync(id, supervisorUserId, role, ct);
        if (!ok || result is null)
        {
            if (err?.Contains("introuvable", StringComparison.OrdinalIgnoreCase) == true)
                return NotFound(new { error = err });
            if (err?.Contains("refusé", StringComparison.OrdinalIgnoreCase) == true)
                return StatusCode(403, new { error = err });
            return BadRequest(new { error = err ?? "Lecture impossible." });
        }

        return Ok(result);
    }
}
