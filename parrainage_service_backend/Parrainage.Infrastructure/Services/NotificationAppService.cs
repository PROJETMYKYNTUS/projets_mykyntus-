using Microsoft.EntityFrameworkCore;
using Parrainage.Application.Abstractions;
using Parrainage.Application.DTOs;
using Parrainage.Infrastructure.Persistence;

namespace Parrainage.Infrastructure.Services;

public sealed class NotificationAppService(
    ParrainageDbContext db,
    ReferralWorkflowService workflow) : INotificationAppService
{
    public async Task<IReadOnlyList<ReferralNotificationDto>> ListAsync(
        string? role,
        string? userId,
        string? projectId,
        CancellationToken ct = default)
    {
        _ = projectId;

        var all = await db.ReferralNotifications.AsNoTracking()
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);

        if (string.IsNullOrWhiteSpace(role))
            return all.Select(n => n.ToDto()).ToList();

        var referrals = await db.Referrals.AsNoTracking().ToListAsync(ct);
        var filtered = await workflow.FilterNotificationsForRoleAsync(all, referrals, role, userId, ct);
        return filtered.Select(n => n.ToDto()).ToList();
    }

    public async Task<NotificationPreferencesDto> GetPreferencesAsync(CancellationToken ct = default)
    {
        var prefs = await LoadPreferencesAsync(ct);
        return prefs.ToDto();
    }

    public async Task<NotificationPreferencesDto> UpdatePreferencesAsync(
        NotificationPreferencesDto body,
        CancellationToken ct = default)
    {
        var prefs = await LoadPreferencesAsync(ct);
        prefs.Email = body.Email;
        prefs.InApp = body.InApp;
        if (body.SystemAlerts.HasValue) prefs.SystemAlerts = body.SystemAlerts.Value;
        if (body.Referrals.HasValue) prefs.Referrals = body.Referrals.Value;
        if (body.Approvals.HasValue) prefs.Approvals = body.Approvals.Value;
        if (body.Payments.HasValue) prefs.Payments = body.Payments.Value;
        await db.SaveChangesAsync(ct);
        return prefs.ToDto();
    }

    public async Task<bool> MarkReadAsync(string id, CancellationToken ct = default)
    {
        var entity = await db.ReferralNotifications.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (entity is null)
            return false;
        entity.Read = true;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task MarkAllReadAsync(CancellationToken ct = default)
    {
        await db.ReferralNotifications.Where(n => !n.Read)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.Read, true), ct);
    }

    private async Task<NotificationPreferenceEntity> LoadPreferencesAsync(CancellationToken ct)
    {
        var prefs = await db.NotificationPreferences.FirstOrDefaultAsync(p => p.Id == 1, ct);
        if (prefs is null)
        {
            prefs = new NotificationPreferenceEntity { Id = 1 };
            db.NotificationPreferences.Add(prefs);
            await db.SaveChangesAsync(ct);
        }

        return prefs;
    }
}
