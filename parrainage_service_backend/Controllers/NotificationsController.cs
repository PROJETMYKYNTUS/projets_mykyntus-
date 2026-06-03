using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParrainageBackend.Data;
using ParrainageBackend.Dto;
using ParrainageBackend.Services;

namespace ParrainageBackend.Controllers;

[ApiController]
[Route("api/parrainage/notifications")]
public sealed class NotificationsController(
    ParrainageDbContext db,
    ReferralWorkflowService workflow,
    IParrainageRequestUserResolver userResolver) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ReferralNotificationDto>>> List(
        [FromQuery] string? role,
        [FromQuery] string? userId,
        [FromQuery] string? projectId,
        CancellationToken ct)
    {
        var resolved = userResolver.Resolve(Request, role, userId, projectId);
        role = resolved.Role;
        userId = resolved.UserId;
        projectId = resolved.ProjectId ?? projectId;

        var all = await db.ReferralNotifications.AsNoTracking()
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);

        if (string.IsNullOrWhiteSpace(role))
            return Ok(all.Select(n => n.ToDto()).ToList());

        var referrals = await db.Referrals.AsNoTracking().ToListAsync(ct);
        var filtered = workflow.FilterNotificationsForRole(all, referrals, role, userId);
        return Ok(filtered.Select(n => n.ToDto()).ToList());
    }

    [HttpGet("preferences")]
    public async Task<ActionResult<NotificationPreferencesDto>> GetPreferences(CancellationToken ct)
    {
        var prefs = await LoadPreferencesAsync(ct);
        return Ok(prefs.ToDto());
    }

    [HttpPatch("preferences")]
    public async Task<ActionResult<NotificationPreferencesDto>> UpdatePreferences([FromBody] NotificationPreferencesDto body, CancellationToken ct)
    {
        var prefs = await LoadPreferencesAsync(ct);
        prefs.Email = body.Email;
        prefs.InApp = body.InApp;
        if (body.SystemAlerts.HasValue) prefs.SystemAlerts = body.SystemAlerts.Value;
        if (body.Referrals.HasValue) prefs.Referrals = body.Referrals.Value;
        if (body.Approvals.HasValue) prefs.Approvals = body.Approvals.Value;
        if (body.Payments.HasValue) prefs.Payments = body.Payments.Value;
        await db.SaveChangesAsync(ct);
        return Ok(prefs.ToDto());
    }

    [HttpPost("read")]
    public async Task<IActionResult> MarkRead([FromBody] MarkReadRequest body, CancellationToken ct)
    {
        var entity = await db.ReferralNotifications.FirstOrDefaultAsync(n => n.Id == body.Id, ct);
        if (entity == null) return NotFound(new { error = $"Notification introuvable : {body.Id}" });
        entity.Read = true;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await db.ReferralNotifications.Where(n => !n.Read).ExecuteUpdateAsync(s => s.SetProperty(n => n.Read, true), ct);
        return NoContent();
    }

    private async Task<NotificationPreferenceEntity> LoadPreferencesAsync(CancellationToken ct)
    {
        var prefs = await db.NotificationPreferences.FirstOrDefaultAsync(p => p.Id == 1, ct);
        if (prefs == null)
        {
            prefs = new NotificationPreferenceEntity { Id = 1 };
            db.NotificationPreferences.Add(prefs);
            await db.SaveChangesAsync(ct);
        }

        return prefs;
    }
}
