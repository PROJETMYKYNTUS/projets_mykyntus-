using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParrainageBackend.Data;
using ParrainageBackend.Dto;
using ParrainageBackend.Services;

namespace ParrainageBackend.Controllers;

[ApiController]
[Route("api/parrainage/audit")]
public sealed class AuditController(ParrainageDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AuditLogDto>>> List([FromQuery] int? take, CancellationToken ct)
    {
        var max = Math.Clamp(take ?? 500, 1, 2000);
        var rows = await db.AuditLogs.AsNoTracking()
            .OrderByDescending(e => e.Timestamp)
            .Take(max)
            .ToListAsync(ct);
        return Ok(rows.Select(e => e.ToDto()).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<AuditLogDto>> Create([FromBody] CreateAuditRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Action))
            return BadRequest(new { error = "action est requise." });

        var entity = new AuditLogEntryEntity
        {
            Id = $"audit-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Action = body.Action,
            UserId = body.UserId ?? "system",
            UserLabel = body.UserLabel ?? "Système",
            Timestamp = DateTimeOffset.UtcNow,
            Details = body.Details,
        };
        db.AuditLogs.Add(entity);
        await db.SaveChangesAsync(ct);
        return Ok(entity.ToDto());
    }
}
