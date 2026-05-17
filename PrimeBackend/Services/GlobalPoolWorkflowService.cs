using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;

namespace PrimeBackend.Services;

/// <summary>Workflow configurable du fichier global des primes (vagues par <see cref="GlobalPoolWorkflowStepEntity.SortOrder"/>).</summary>
public sealed class GlobalPoolWorkflowService(PrimeDbContext db)
{
    public static bool RolesEqual(string a, string b) => IPrimeRequestUserResolver.RolesMatch(a, b);

    public async Task<bool> UsesConfigurableWorkflowAsync(CancellationToken ct) =>
        await db.GlobalPoolWorkflowSteps.AnyAsync(s => s.IsActive, ct);

    public async Task<List<GlobalPoolWorkflowStepEntity>> ListActiveStepsAsync(CancellationToken ct) =>
        await db.GlobalPoolWorkflowSteps.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.ApproverRole)
            .ToListAsync(ct);

    public bool LegacyPoolUnlocked(SupervisorCellulePrimeDraftEntity d) =>
        d.GlobalPoolManagerApprovedAt.HasValue && d.GlobalPoolRhApprovedAt.HasValue;

    public async Task<bool> PoolDistributionUnlockedAsync(SupervisorCellulePrimeDraftEntity d, CancellationToken ct)
    {
        if (!await UsesConfigurableWorkflowAsync(ct))
            return LegacyPoolUnlocked(d);
        await SyncLegacyToApprovalsIfNeededAsync(d, ct);
        var steps = await ListActiveStepsAsync(ct);
        if (steps.Count == 0) return LegacyPoolUnlocked(d);
        var waves = steps.GroupBy(s => s.SortOrder).OrderBy(g => g.Key);
        foreach (var wave in waves)
        {
            foreach (var st in wave.Where(x => x.IsRequired))
            {
                var ok = await db.GlobalPoolApprovals.AsNoTracking()
                    .AnyAsync(a => a.DraftId == d.Id && a.StepId == st.Id, ct);
                if (!ok) return false;
            }
        }
        return true;
    }

    /// <summary>Recopie les timestamps historiques vers <see cref="GlobalPoolApprovalEntity"/> une seule fois.</summary>
    public async Task SyncLegacyToApprovalsIfNeededAsync(SupervisorCellulePrimeDraftEntity d, CancellationToken ct)
    {
        if (!await UsesConfigurableWorkflowAsync(ct)) return;
        if (await db.GlobalPoolApprovals.AnyAsync(a => a.DraftId == d.Id, ct)) return;
        var steps = await db.GlobalPoolWorkflowSteps.AsNoTracking().Where(s => s.IsActive).ToListAsync(ct);
        foreach (var st in steps)
        {
            if (RolesEqual(st.ApproverRole, "Manager") && d.GlobalPoolManagerApprovedAt is { } t1 && !string.IsNullOrEmpty(d.GlobalPoolManagerApprovedByUserId))
                db.GlobalPoolApprovals.Add(new GlobalPoolApprovalEntity
                {
                    Id = Guid.NewGuid(),
                    DraftId = d.Id,
                    StepId = st.Id,
                    UserId = d.GlobalPoolManagerApprovedByUserId!,
                    ApprovedAt = t1,
                });
            if (RolesEqual(st.ApproverRole, "RH") && d.GlobalPoolRhApprovedAt is { } t2 && !string.IsNullOrEmpty(d.GlobalPoolRhApprovedByUserId))
                db.GlobalPoolApprovals.Add(new GlobalPoolApprovalEntity
                {
                    Id = Guid.NewGuid(),
                    DraftId = d.Id,
                    StepId = st.Id,
                    UserId = d.GlobalPoolRhApprovedByUserId!,
                    ApprovedAt = t2,
                });
            if ((RolesEqual(st.ApproverRole, "Comptabilité") || RolesEqual(st.ApproverRole, "Comptable"))
                && d.GlobalPoolComptaAckAt is { } t3 && !string.IsNullOrEmpty(d.GlobalPoolComptaAckByUserId))
                db.GlobalPoolApprovals.Add(new GlobalPoolApprovalEntity
                {
                    Id = Guid.NewGuid(),
                    DraftId = d.Id,
                    StepId = st.Id,
                    UserId = d.GlobalPoolComptaAckByUserId!,
                    ApprovedAt = t3,
                });
        }
        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(ct);
    }

    public async Task<bool> PendingActionForUserAsync(SupervisorCellulePrimeDraftEntity d, string employeeRole, CancellationToken ct)
    {
        if (d.GlobalPoolExcelContent is not { Length: > 0 }) return false;
        if (!await UsesConfigurableWorkflowAsync(ct))
        {
            return employeeRole switch
            {
                "Manager" => !d.GlobalPoolManagerApprovedAt.HasValue,
                "RH" => !d.GlobalPoolRhApprovedAt.HasValue,
                "Comptable" or "Comptabilité" => LegacyPoolUnlocked(d) && !d.GlobalPoolComptaAckAt.HasValue,
                "Admin" => !d.GlobalPoolManagerApprovedAt.HasValue || !d.GlobalPoolRhApprovedAt.HasValue ||
                           (LegacyPoolUnlocked(d) && !d.GlobalPoolComptaAckAt.HasValue),
                _ => false,
            };
        }
        await SyncLegacyToApprovalsIfNeededAsync(d, ct);
        var steps = await ListActiveStepsAsync(ct);
        var waves = steps.GroupBy(s => s.SortOrder).OrderBy(g => g.Key);
        foreach (var wave in waves)
        {
            var pendingInWave = new List<GlobalPoolWorkflowStepEntity>();
            foreach (var st in wave.Where(x => x.IsRequired))
            {
                var done = await db.GlobalPoolApprovals.AsNoTracking()
                    .AnyAsync(a => a.DraftId == d.Id && a.StepId == st.Id, ct);
                if (!done) pendingInWave.Add(st);
            }
            if (pendingInWave.Count == 0) continue;
            return pendingInWave.Any(st => RolesEqual(st.ApproverRole, employeeRole) || employeeRole == "Admin");
        }
        return false;
    }

    /// <summary>Étapes actives et date d’approbation éventuelle (workflow configurable uniquement).</summary>
    public async Task<List<GlobalPoolInboxStepStatusDto>> ListInboxStepStatusesAsync(
        SupervisorCellulePrimeDraftEntity d,
        CancellationToken ct)
    {
        if (!await UsesConfigurableWorkflowAsync(ct))
            return [];
        await SyncLegacyToApprovalsIfNeededAsync(d, ct);
        var steps = await ListActiveStepsAsync(ct);
        if (steps.Count == 0)
            return [];
        var approvals = await db.GlobalPoolApprovals.AsNoTracking()
            .Where(a => a.DraftId == d.Id)
            .ToListAsync(ct);
        var byStep = approvals.ToDictionary(a => a.StepId, a => a.ApprovedAt);
        return steps.Select(s => new GlobalPoolInboxStepStatusDto
        {
            StepId = s.Id,
            SortOrder = s.SortOrder,
            ApproverRole = s.ApproverRole,
            IsRequired = s.IsRequired,
            ApprovedAt = byStep.TryGetValue(s.Id, out var at) ? at : null,
        }).ToList();
    }

    /// <summary>Première étape encore requise dans la première vague incomplète, pour le rôle donné (Admin : première étape requise quel que soit le rôle).</summary>
    public async Task<Guid?> GetSuggestedApproveStepIdAsync(
        SupervisorCellulePrimeDraftEntity d,
        string employeeRole,
        CancellationToken ct)
    {
        if (d.GlobalPoolExcelContent is not { Length: > 0 }) return null;
        if (!await UsesConfigurableWorkflowAsync(ct)) return null;
        await SyncLegacyToApprovalsIfNeededAsync(d, ct);
        var steps = await ListActiveStepsAsync(ct);
        if (steps.Count == 0) return null;
        var waves = steps.GroupBy(s => s.SortOrder).OrderBy(g => g.Key);
        foreach (var wave in waves)
        {
            var pendingRequired = new List<GlobalPoolWorkflowStepEntity>();
            foreach (var st in wave.Where(x => x.IsRequired))
            {
                var done = await db.GlobalPoolApprovals.AsNoTracking()
                    .AnyAsync(a => a.DraftId == d.Id && a.StepId == st.Id, ct);
                if (!done) pendingRequired.Add(st);
            }
            if (pendingRequired.Count == 0) continue;
            if (string.Equals(employeeRole, "Admin", StringComparison.Ordinal))
                return pendingRequired[0].Id;
            var mine = pendingRequired.FirstOrDefault(st => RolesEqual(st.ApproverRole, employeeRole));
            return mine?.Id;
        }
        return null;
    }

    public async Task<(bool ok, string? error)> TryApproveStepAsync(
        SupervisorCellulePrimeDraftEntity d,
        Guid stepId,
        string userId,
        string employeeRole,
        CancellationToken ct)
    {
        if (d.GlobalPoolExcelContent is not { Length: > 0 })
            return (false, "Aucun fichier de synthèse globale sur ce brouillon.");
        if (!await UsesConfigurableWorkflowAsync(ct))
            return (false, "Workflow global non configuré — utiliser les routes historiques.");
        var step = await db.GlobalPoolWorkflowSteps.FirstOrDefaultAsync(s => s.Id == stepId && s.IsActive, ct);
        if (step is null) return (false, "Étape inconnue ou inactive.");
        if (!string.Equals(employeeRole, "Admin", StringComparison.Ordinal) && !RolesEqual(step.ApproverRole, employeeRole))
            return (false, "Ce rôle ne correspond pas à cette étape.");
        if (await db.GlobalPoolApprovals.AnyAsync(a => a.DraftId == d.Id && a.StepId == stepId, ct))
            return (false, "Cette étape est déjà validée.");

        var priorWaves = await ListActiveStepsAsync(ct);
        var waveOrder = step.SortOrder;
        foreach (var w in priorWaves.Where(s => s.SortOrder < waveOrder).GroupBy(s => s.SortOrder).OrderBy(g => g.Key))
        {
            foreach (var st in w.Where(x => x.IsRequired))
            {
                var done = await db.GlobalPoolApprovals.AsNoTracking()
                    .AnyAsync(a => a.DraftId == d.Id && a.StepId == st.Id, ct);
                if (!done)
                    return (false, "Les étapes des vagues précédentes doivent être complétées avant celle-ci.");
            }
        }
        foreach (var st in priorWaves.Where(s => s.SortOrder == waveOrder && s.Id != stepId && s.IsRequired))
        {
            if (string.Equals(st.ApproverRole, step.ApproverRole, StringComparison.Ordinal)) continue;
            var done = await db.GlobalPoolApprovals.AsNoTracking()
                .AnyAsync(a => a.DraftId == d.Id && a.StepId == st.Id, ct);
            if (!done && st.SortOrder == waveOrder)
            {
                // même vague : les autres rôles de la vague peuvent être en parallèle — pas d’ordre interne
            }
        }

        var now = DateTimeOffset.UtcNow;
        db.GlobalPoolApprovals.Add(new GlobalPoolApprovalEntity
        {
            Id = Guid.NewGuid(),
            DraftId = d.Id,
            StepId = step.Id,
            UserId = userId.Trim(),
            ApprovedAt = now,
        });
        if (RolesEqual(step.ApproverRole, "Manager"))
        {
            d.GlobalPoolManagerApprovedAt = now;
            d.GlobalPoolManagerApprovedByUserId = userId.Trim();
        }
        if (RolesEqual(step.ApproverRole, "RH"))
        {
            d.GlobalPoolRhApprovedAt = now;
            d.GlobalPoolRhApprovedByUserId = userId.Trim();
        }
        if (RolesEqual(step.ApproverRole, "Comptable") || RolesEqual(step.ApproverRole, "Comptabilité"))
        {
            d.GlobalPoolComptaAckAt = now;
            d.GlobalPoolComptaAckByUserId = userId.Trim();
        }
        d.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return (true, null);
    }
}
