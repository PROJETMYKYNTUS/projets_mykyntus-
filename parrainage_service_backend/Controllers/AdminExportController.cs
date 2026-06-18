using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParrainageBackend.Data;
using ParrainageBackend.Dto;
using ParrainageBackend.Services;

namespace ParrainageBackend.Controllers;

[ApiController]
[Route("api/parrainage/admin")]
public sealed class AdminExportController(ParrainageDbContext db) : ControllerBase
{
    [HttpGet("export")]
    public async Task<ActionResult<ExportSnapshotDto>> Export(CancellationToken ct)
    {
        var referrals = await db.Referrals.AsNoTracking().OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        var rules = await db.ReferralRules.AsNoTracking().OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        var history = await db.ReferralHistory.AsNoTracking().OrderByDescending(h => h.CreatedAt).ToListAsync(ct);
        var notifications = await db.ReferralNotifications.AsNoTracking().OrderByDescending(n => n.CreatedAt).ToListAsync(ct);
        var prefs = await db.NotificationPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.Id == 1, ct);
        var cfg = await db.SystemConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.Id == 1, ct);
        var audit = await db.AuditLogs.AsNoTracking().OrderByDescending(a => a.Timestamp).Take(500).ToListAsync(ct);

        return Ok(new ExportSnapshotDto
        {
            ExportedAt = DateTimeOffset.UtcNow.ToString("o"),
            Referrals = referrals.Select(r => r.ToDto()).ToList(),
            Rules = rules.Select(r => r.ToDto()).ToList(),
            History = history.Select(h => h.ToDto()).ToList(),
            Notifications = notifications.Select(n => n.ToDto()).ToList(),
            NotificationPreferences = prefs?.ToDto(),
            SystemConfig = cfg?.ToDto(),
            AuditLog = audit.Select(a => a.ToDto()).ToList(),
        });
    }
}
