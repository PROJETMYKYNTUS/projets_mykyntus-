using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Planning.Application.Abstractions;
using Planning.Application.DTOs.Planning;
using Planning.Domain.Entities;
using Planning.Domain.Enums;
using Planning.Infrastructure.Persistence;

namespace Planning.Infrastructure.Services;

public sealed class PlanningLeaveImpactService(
    AppDbContext db,
    IPlanningService planningService,
    ILogger<PlanningLeaveImpactService> logger) : IPlanningLeaveImpactService
{
    public async Task SyncAfterAbsenceChangeAsync(
        int userId,
        DateOnly start,
        DateOnly end,
        bool absenceRemoved,
        CancellationToken ct = default)
    {
        if (end < start)
            (start, end) = (end, start);

        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user?.SubServiceId is not int subServiceId || subServiceId <= 0)
        {
            logger.LogDebug(
                "Sync congé ignoré : utilisateur {UserId} sans SubServiceId.",
                userId);
            return;
        }

        var plannings = await db.WeeklyPlannings
            .Include(p => p.ShiftAssignments)
            .Include(p => p.SubService)
            .Where(p =>
                p.SubServiceId == subServiceId
                && p.Status != PlanningStatus.Archived
                && p.WeekStartDate <= end
                && p.WeekStartDate.AddDays(5) >= start)
            .ToListAsync(ct);

        if (plannings.Count == 0)
        {
            logger.LogDebug(
                "Aucun planning existant à synchroniser pour user {UserId} ({Start}→{End}).",
                userId, start, end);
            return;
        }

        foreach (var planning in plannings)
        {
            try
            {
                await SyncOneWeekAsync(planning, userId, start, end, absenceRemoved, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Échec sync congé planning {PlanningId} (semaine {WeekCode}) pour user {UserId}.",
                    planning.Id,
                    planning.WeekCode,
                    userId);
            }
        }
    }

    private async Task SyncOneWeekAsync(
        WeeklyPlanning planning,
        int userId,
        DateOnly start,
        DateOnly end,
        bool absenceRemoved,
        CancellationToken ct)
    {
        var regenerateFrom = PlanningRegenWindow.GetEarliestRegenerableDate(DateTime.Now);
        var shiftConfigs = await LoadShiftConfigsAsync(planning.SubServiceId, planning.WeekCode, ct);

        // Jours figés (passé / jour J / demain post-15h) → toujours chirurgical
        if (!absenceRemoved)
            await ApplySurgicalLeaveAsync(planning, userId, start, end, ct, onlyBefore: regenerateFrom);
        else
            await ApplySurgicalRestoreAsync(planning, userId, start, end, ct, onlyBefore: regenerateFrom);

        // Recharger assignments après chirurgical (tracking)
        await db.Entry(planning).Collection(p => p.ShiftAssignments).LoadAsync(ct);

        var needsRegen = PlanningLeaveImpactEvaluator.NeedsRegen(
            planning, shiftConfigs, userId, start, end, absenceRemoved, regenerateFrom);

        if (needsRegen)
        {
            var weekSat = planning.WeekStartDate.AddDays(5);
            if (regenerateFrom > weekSat)
            {
                logger.LogInformation(
                    "Congé → pas de regen (deadline 15h, aucun jour ouvert) planning {PlanningId}.",
                    planning.Id);
                if (!absenceRemoved)
                    await ApplySurgicalLeaveAsync(planning, userId, start, end, ct, onlyBefore: null);
                return;
            }

            var reason = absenceRemoved
                ? $"Annulation / refus d'absence ({start:dd/MM}–{end:dd/MM})"
                : $"Absence / congé validé ({start:dd/MM}–{end:dd/MM})";

            logger.LogInformation(
                "Congé → regen partielle planning {PlanningId} ({WeekCode}) from {From}, absenceRemoved={Removed} (repasse en Draft à revalider).",
                planning.Id,
                planning.WeekCode,
                regenerateFrom,
                absenceRemoved);

            await planningService.GeneratePlanningFromConfigAsync(new GeneratePlanningFromConfigDto
            {
                SubServiceId = planning.SubServiceId,
                WeekCode = planning.WeekCode,
                WeeklyPlanningId = planning.Id,
                RegenerateFromDate = regenerateFrom,
                RepublishReason = reason
            });
            // Pas de republication auto : statut repasse en Draft, validation RH requise.
            return;
        }

        if (absenceRemoved)
            await ApplySurgicalRestoreAsync(planning, userId, start, end, ct, onlyBefore: null);
        else
            await ApplySurgicalLeaveAsync(planning, userId, start, end, ct, onlyBefore: null);

        logger.LogInformation(
            "Congé → sync chirurgicale planning {PlanningId} ({WeekCode}), absenceRemoved={Removed}.",
            planning.Id,
            planning.WeekCode,
            absenceRemoved);
    }

    private async Task<List<SubServiceShiftConfig>> LoadShiftConfigsAsync(
        int subServiceId,
        string weekCode,
        CancellationToken ct)
    {
        var snapshot = await db.SubServiceShiftConfigs
            .AsNoTracking()
            .Where(c => c.SubServiceId == subServiceId && c.WeekCode == weekCode && !c.IsTemplate)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync(ct);

        if (snapshot.Count > 0)
            return snapshot;

        return await db.SubServiceShiftConfigs
            .AsNoTracking()
            .Where(c => c.SubServiceId == subServiceId && c.IsTemplate)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync(ct);
    }

    /// <param name="onlyBefore">Si défini, n'applique que sur AssignedDate &lt; onlyBefore.</param>
    private async Task ApplySurgicalLeaveAsync(
        WeeklyPlanning planning,
        int userId,
        DateOnly start,
        DateOnly end,
        CancellationToken ct,
        DateOnly? onlyBefore)
    {
        var weekStart = planning.WeekStartDate;
        var weekEnd = weekStart.AddDays(5);
        var overlapStart = start > weekStart ? start : weekStart;
        var overlapEnd = end < weekEnd ? end : weekEnd;

        var rows = planning.ShiftAssignments
            .Where(a =>
                a.UserId == userId
                && a.AssignedDate >= overlapStart
                && a.AssignedDate <= overlapEnd
                && !a.IsHoliday
                && (onlyBefore is null || a.AssignedDate < onlyBefore.Value))
            .ToList();

        var changed = false;
        foreach (var a in rows)
        {
            if (a.IsOnLeave) continue;
            a.IsOnLeave = true;
            a.BreakTime = null;
            a.ShiftId = null;
            changed = true;
        }

        if (changed)
            await db.SaveChangesAsync(ct);
    }

    private async Task ApplySurgicalRestoreAsync(
        WeeklyPlanning planning,
        int userId,
        DateOnly start,
        DateOnly end,
        CancellationToken ct,
        DateOnly? onlyBefore)
    {
        var weekStart = planning.WeekStartDate;
        var weekEnd = weekStart.AddDays(5);
        var overlapStart = start > weekStart ? start : weekStart;
        var overlapEnd = end < weekEnd ? end : weekEnd;

        var rows = planning.ShiftAssignments
            .Where(a =>
                a.UserId == userId
                && a.IsOnLeave
                && a.AssignedDate >= overlapStart
                && a.AssignedDate <= overlapEnd
                && (onlyBefore is null || a.AssignedDate < onlyBefore.Value))
            .ToList();

        var changed = false;
        foreach (var a in rows)
        {
            if (a.SubServiceShiftConfigId is null) continue;
            a.IsOnLeave = false;
            changed = true;
        }

        if (changed)
            await db.SaveChangesAsync(ct);
    }
}
