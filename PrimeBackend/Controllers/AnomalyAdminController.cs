using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;
using PrimeBackend.Services;

namespace PrimeBackend.Controllers;

/// <summary>API des anomalies détectées sur les fiches PRIME (Phase 1.4 + 1.5).</summary>
[ApiController]
[Route("api/prime/admin/anomalies")]
public sealed class AnomalyAdminController(PrimeDbContext? db, AnomalyDetectionService? detection = null) : ControllerBase
{
    private static AnomalyDto Map(AnomalyEntity e) => new()
    {
        Id = e.Id,
        DetectedAt = e.DetectedAt,
        UpdatedAt = e.UpdatedAt,
        Type = e.Type,
        Severity = e.Severity,
        Status = e.Status,
        Description = e.Description,
        TargetEntityType = e.TargetEntityType,
        TargetEntityId = e.TargetEntityId,
        Period = e.Period,
        ServiceId = e.ServiceId,
        CelluleId = e.CelluleId,
        PoleId = e.PoleId,
        ContextJson = e.ContextJson,
        ResolvedByUserId = e.ResolvedByUserId,
        ResolvedAt = e.ResolvedAt,
        ResolutionNote = e.ResolutionNote,
    };

    [HttpGet]
    public async Task<ActionResult<List<AnomalyDto>>> List(
        [FromQuery] string? status,
        [FromQuery] string? type,
        [FromQuery] string? severity,
        [FromQuery] string? period,
        [FromQuery] string? serviceId,
        [FromQuery] string? celluleId,
        [FromQuery] string? poleId,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var q = db.Anomalies.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(a => a.Status == status.Trim());
        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(a => a.Type == type.Trim());
        if (!string.IsNullOrWhiteSpace(severity)) q = q.Where(a => a.Severity == severity.Trim());
        if (!string.IsNullOrWhiteSpace(period)) q = q.Where(a => a.Period == period.Trim());
        if (!string.IsNullOrWhiteSpace(serviceId)) q = q.Where(a => a.ServiceId == serviceId.Trim());
        if (!string.IsNullOrWhiteSpace(celluleId)) q = q.Where(a => a.CelluleId == celluleId.Trim());
        if (!string.IsNullOrWhiteSpace(poleId)) q = q.Where(a => a.PoleId == poleId.Trim());
        var rows = await q.OrderByDescending(a => a.DetectedAt).Take(500).ToListAsync(ct);
        return Ok(rows.Select(Map).ToList());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AnomalyDto>> UpdateStatus(Guid id, [FromBody] UpdateAnomalyStatusBody body, CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(body.Status)) return BadRequest(new { error = "Status est obligatoire." });
        var allowed = new[] { "Open", "InReview", "Resolved", "Ignored" };
        if (!allowed.Contains(body.Status)) return BadRequest(new { error = "Status invalide." });

        var row = await db.Anomalies.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (row == null) return NotFound();
        var now = DateTimeOffset.UtcNow;
        row.Status = body.Status;
        row.UpdatedAt = now;
        if (body.Status is "Resolved" or "Ignored")
        {
            row.ResolvedAt = now;
            row.ResolvedByUserId = body.ResolvedByUserId;
            row.ResolutionNote = body.ResolutionNote;
        }
        else
        {
            row.ResolvedAt = null;
            row.ResolvedByUserId = null;
            row.ResolutionNote = null;
        }
        await db.SaveChangesAsync(ct);
        return Ok(Map(row));
    }

    [HttpPost("recompute-all")]
    public async Task<ActionResult<object>> RecomputeAll(CancellationToken ct)
    {
        if (db == null || detection == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var n = await detection.RecomputeAllAsync(ct);
        return Ok(new { upsertedCount = n });
    }
}
