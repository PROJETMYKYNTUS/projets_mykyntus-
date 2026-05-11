using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;
using PrimeBackend.Services;

namespace PrimeBackend.Controllers;

[ApiController]
[Route("api/prime/cellules/{celluleId}/prime-indicators")]
public sealed class CellulePrimeIndicatorsController(PrimeDbContext? db, PrimeInMemoryStore store) : ControllerBase
{
    private static CellulePrimeIndicatorDto Map(CellulePrimeIndicatorEntity e) =>
        new()
        {
            Id = e.Id,
            CelluleId = e.CelluleId,
            SortOrder = e.SortOrder,
            Label = e.Label,
            PonderationPrimePct = e.PonderationPrimePct,
            PonderationChallengePct = e.PonderationChallengePct,
            IsActive = e.IsActive,
            TemplateStableId = e.TemplateStableId,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt,
        };

    [HttpGet]
    public async Task<ActionResult<List<CellulePrimeIndicatorDto>>> Get(
        string celluleId,
        [FromQuery] string supervisorUserId,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(supervisorUserId)) return BadRequest(new { error = "supervisorUserId requis." });

        var poleId = store.GetPoleIdForCellule(celluleId);
        if (poleId is null) return NotFound(new { error = "Cellule introuvable." });
        if (!store.SupervisorOwnsPole(supervisorUserId, poleId))
            return StatusCode(403, new { error = "Accès refusé pour ce périmètre." });

        var list = await db.CellulePrimeIndicators.AsNoTracking()
            .Where(x => x.CelluleId == celluleId.Trim())
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);
        return Ok(list.ConvertAll(Map));
    }

    [HttpPut]
    public async Task<ActionResult<List<CellulePrimeIndicatorDto>>> Put(
        string celluleId,
        [FromQuery] string supervisorUserId,
        [FromBody] PutCellulePrimeIndicatorsRequest body,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(supervisorUserId)) return BadRequest(new { error = "supervisorUserId requis." });

        var poleId = store.GetPoleIdForCellule(celluleId);
        if (poleId is null) return NotFound(new { error = "Cellule introuvable." });
        if (!store.SupervisorOwnsPole(supervisorUserId, poleId))
            return StatusCode(403, new { error = "Accès refusé pour ce périmètre." });

        var cid = celluleId.Trim();
        var now = DateTimeOffset.UtcNow;
        var existing = await db.CellulePrimeIndicators.Where(x => x.CelluleId == cid).ToListAsync(ct);
        db.CellulePrimeIndicators.RemoveRange(existing);

        foreach (var item in body.Indicators.OrderBy(i => i.SortOrder))
        {
            db.CellulePrimeIndicators.Add(new CellulePrimeIndicatorEntity
            {
                Id = Guid.NewGuid(),
                CelluleId = cid,
                SortOrder = item.SortOrder,
                Label = item.Label.Trim(),
                PonderationPrimePct = item.PonderationPrimePct,
                PonderationChallengePct = item.PonderationChallengePct,
                IsActive = item.IsActive,
                TemplateStableId = string.IsNullOrWhiteSpace(item.TemplateStableId) ? null : item.TemplateStableId.Trim(),
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        await db.SaveChangesAsync(ct);
        var list = await db.CellulePrimeIndicators.AsNoTracking()
            .Where(x => x.CelluleId == cid)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);
        return Ok(list.ConvertAll(Map));
    }
}
