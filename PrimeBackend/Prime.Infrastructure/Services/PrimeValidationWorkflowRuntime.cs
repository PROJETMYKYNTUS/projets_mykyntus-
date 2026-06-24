using Microsoft.EntityFrameworkCore;
using Prime.Domain.Entities;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Services;

/// <summary>
/// Workflow fiches : transitions issues uniquement de <see cref="WorkflowStepConfig"/> (aucun ordre codé en dur).
/// </summary>
public sealed class PrimeValidationWorkflowRuntime(PrimeDbContext db)
{
    public async Task<WorkflowStepConfig?> FindActiveStepAsync(string fromStatus, string approverRole, CancellationToken ct)
    {
        var candidates = await db.WorkflowSteps.AsNoTracking()
            .Where(s => s.IsActive && s.FromStatus == fromStatus)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);
        return candidates.FirstOrDefault(s => PrimeRbacReadService.RolesMatchWorkflowApprover(approverRole, s.ApproverRole));
    }

    public async Task<(bool ok, string? error, string? nextStatus, WorkflowStepConfig? step)> TryResolveApprovalAsync(
        EmployeePrimeServiceFiche fiche,
        string approverRole,
        CancellationToken ct)
    {
        var step = await FindActiveStepAsync(fiche.ValidationStatus, approverRole, ct);
        if (step is null)
            return (false,
                $"Aucune étape active du workflow ne permet au rôle « {approverRole} » de valider depuis « {fiche.ValidationStatus} ».",
                null, null);
        return (true, null, step.ToStatus, step);
    }

    public async Task<bool> CanRejectAsync(string validationStatus, string rejecterRole, CancellationToken ct)
    {
        if (string.Equals(validationStatus, PrimeValidationWorkflowService.Rejected, StringComparison.Ordinal))
            return false;
        if (await IsTerminalStatusAsync(validationStatus, ct))
            return false;
        var steps = await db.WorkflowSteps.AsNoTracking()
            .Where(s => s.IsActive && s.FromStatus == validationStatus)
            .ToListAsync(ct);
        return steps.Any(s => PrimeRbacReadService.RolesMatchWorkflowApprover(rejecterRole, s.ApproverRole));
    }

    /// <summary>Pas d’arête sortante active (ou Rejected). Hors circuit workflow : jamais terminal.</summary>
    public async Task<bool> IsTerminalStatusAsync(string status, CancellationToken ct)
    {
        if (PrimeValidationWorkflowService.IsPreWorkflowStatus(status))
            return false;
        if (string.Equals(status, PrimeValidationWorkflowService.Rejected, StringComparison.Ordinal))
            return true;
        var anySteps = await db.WorkflowSteps.AsNoTracking().AnyAsync(s => s.IsActive, ct);
        if (!anySteps) return false;
        var hasOutgoing = await db.WorkflowSteps.AsNoTracking()
            .AnyAsync(s => s.IsActive && s.FromStatus == status, ct);
        return !hasOutgoing;
    }

    public async Task<bool> IsValidFilterStatusAsync(string status, CancellationToken ct)
    {
        if (string.Equals(status, PrimeValidationWorkflowService.AwaitingData, StringComparison.Ordinal))
            return true;
        if (string.Equals(status, PrimeValidationWorkflowService.Rejected, StringComparison.Ordinal))
            return true;
        var inWorkflow = await db.WorkflowSteps.AsNoTracking()
            .AnyAsync(s => s.IsActive && (s.FromStatus == status || s.ToStatus == status), ct);
        if (inWorkflow) return true;
        return await db.EmployeePrimeServiceFiches.AsNoTracking().AnyAsync(f => f.ValidationStatus == status, ct);
    }

    public async Task<List<string>> GetTerminalStatusesAsync(CancellationToken ct)
    {
        var active = await db.WorkflowSteps.AsNoTracking().Where(s => s.IsActive).ToListAsync(ct);
        if (active.Count == 0)
            return [PrimeValidationWorkflowService.Rejected];
        var from = active.Select(s => s.FromStatus).ToHashSet(StringComparer.Ordinal);
        var terminals = new HashSet<string>(StringComparer.Ordinal)
        {
            PrimeValidationWorkflowService.Rejected,
        };
        foreach (var s in active.Where(x => x.TerminalApproved).Select(x => x.ToStatus))
            terminals.Add(s);
        foreach (var s in active.Select(x => x.ToStatus).Distinct())
        {
            if (!from.Contains(s))
                terminals.Add(s);
        }
        return terminals.ToList();
    }

    /// <summary>Statuts d’entrée des étapes actives pour un rôle valideur (après normalisation RP → Chef de projet).</summary>
    public async Task<List<string>> GetActionableFromStatusesForRoleAsync(string approverRole, CancellationToken ct)
    {
        var steps = await db.WorkflowSteps.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);
        return steps
            .Where(s => PrimeRbacReadService.RolesMatchWorkflowApprover(approverRole, s.ApproverRole))
            .Select(s => s.FromStatus)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public async Task<int?> GetSlaHoursForCurrentStepAsync(string fromStatus, CancellationToken ct)
    {
        var step = await db.WorkflowSteps.AsNoTracking()
            .Where(s => s.IsActive && s.FromStatus == fromStatus)
            .OrderBy(s => s.SortOrder)
            .FirstOrDefaultAsync(ct);
        if (step is null) return null;
        return step.SlaHours > 0 ? step.SlaHours : null;
    }
}
