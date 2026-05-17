using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;
using PrimeBackend.Services;

namespace PrimeBackend.Controllers;

/// <summary>API de consultation du journal d'audit (Phase 1.4).</summary>
[ApiController]
[Route("api/prime/admin/audit-logs")]
public sealed class AuditLogAdminController(PrimeDbContext? db, PrimeAuditLogService auditWriter) : ControllerBase
{
    private static AuditLogDto Map(AuditLogEntity e) => new()
    {
        Id = e.Id,
        At = e.At,
        UserId = e.UserId,
        UserDisplayName = e.UserDisplayName,
        Role = e.Role,
        Action = e.Action,
        EntityType = e.EntityType,
        EntityId = e.EntityId,
        DetailJson = e.DetailJson,
        IpAddress = e.IpAddress,
    };

    [HttpGet]
    public async Task<ActionResult<List<AuditLogDto>>> List(
        [FromQuery] string? userId,
        [FromQuery] string? role,
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int? take,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var q = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(userId)) q = q.Where(l => l.UserId == userId.Trim());
        if (!string.IsNullOrWhiteSpace(role)) q = q.Where(l => l.Role == role.Trim());
        if (!string.IsNullOrWhiteSpace(action)) q = q.Where(l => l.Action == action.Trim());
        if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(l => l.EntityType == entityType.Trim());
        if (!string.IsNullOrWhiteSpace(entityId)) q = q.Where(l => l.EntityId == entityId.Trim());
        if (from.HasValue) q = q.Where(l => l.At >= from.Value);
        if (to.HasValue) q = q.Where(l => l.At <= to.Value);
        q = q.OrderByDescending(l => l.At);
        var max = Math.Clamp(take ?? 200, 1, 1000);
        var rows = await q.Take(max).ToListAsync(ct);
        return Ok(rows.Select(Map).ToList());
    }

    /// <summary>Enregistre une consultation d’écran (SPA) pour la supervision admin.</summary>
    [HttpPost("nav")]
    public async Task<IActionResult> RecordNavigation([FromBody] RecordAuditNavigationRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.UserId) || string.IsNullOrWhiteSpace(body.Route))
            return BadRequest(new { error = "userId et route sont requis." });
        await auditWriter.RecordNavigationAsync(body.UserId, body.UserDisplayName, body.Role, body.Route, ct);
        return NoContent();
    }
}
