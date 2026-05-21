using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;

namespace PrimeBackend.Services;

/// <summary>
/// Soumission automatique des fiches employé au workflow de validation lorsque
/// la partie commune est validée (brouillon <c>Validated</c>) et la partie cellule est complète.
/// </summary>
public sealed class PrimeFicheValidationSubmissionService(PrimeDbContext db, PrimeValidationWorkflowRuntime wfRuntime)
{
    public static bool IsDraftValidated(string? draftStatus) =>
        string.Equals(draftStatus?.Trim(), "Validated", StringComparison.OrdinalIgnoreCase);

    public static bool IsCellPartComplete(string? fillingStatus) =>
        string.Equals(fillingStatus?.Trim(), "Complete", StringComparison.OrdinalIgnoreCase);

    public static bool IsReadyForValidation(
        SupervisorCellulePrimeDraftEntity draft,
        EmployeePrimeServiceFicheEntity fiche) =>
        IsDraftValidated(draft.Status) && IsCellPartComplete(fiche.FillingStatus);

    public static bool ComputeIsReadyForValidation(
        SupervisorCellulePrimeDraftEntity draft,
        EmployeePrimeServiceFicheEntity fiche) => IsReadyForValidation(draft, fiche);

    private static bool IsPreSubmissionStatus(string? validationStatus)
    {
        var s = validationStatus?.Trim() ?? "";
        return string.Equals(s, PrimeValidationWorkflowService.AwaitingData, StringComparison.Ordinal) ||
               string.Equals(s, "NotStarted", StringComparison.OrdinalIgnoreCase) ||
               string.IsNullOrEmpty(s);
    }

    public async Task SyncValidationSubmissionStatusAsync(
        EmployeePrimeServiceFicheEntity fiche,
        SupervisorCellulePrimeDraftEntity draft,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        if (string.Equals(fiche.ValidationStatus, PrimeValidationWorkflowService.Rejected, StringComparison.Ordinal))
            return;
        if (await wfRuntime.IsTerminalStatusAsync(fiche.ValidationStatus, ct))
            return;

        var ready = IsReadyForValidation(draft, fiche);

        if (ready)
        {
            if (IsPreSubmissionStatus(fiche.ValidationStatus))
            {
                fiche.ValidationStatus = PrimeValidationWorkflowService.Pending;
                fiche.UpdatedAt = now;
            }

            return;
        }

        if (string.Equals(fiche.ValidationStatus, PrimeValidationWorkflowService.Pending, StringComparison.Ordinal) &&
            fiche.LastApprovedAt is null)
        {
            fiche.ValidationStatus = PrimeValidationWorkflowService.AwaitingData;
            fiche.UpdatedAt = now;
        }
    }

    public async Task SyncForDraftAsync(Guid draftId, CancellationToken ct = default)
    {
        var draft = await db.SupervisorCellulePrimeDrafts.FirstOrDefaultAsync(x => x.Id == draftId, ct);
        if (draft is null) return;

        var fiches = await db.EmployeePrimeServiceFiches
            .Where(f => f.CellulePrimeDraftId == draftId)
            .ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;
        foreach (var fiche in fiches)
            await SyncValidationSubmissionStatusAsync(fiche, draft, now, ct);

        await ReconcileOrphanReadyFichesForDraftAsync(draft, now, ct);
    }

    /// <summary>
    /// Fiches complètes (même superviseur / cellule / période) encore liées à un autre brouillon ou sans soumission.
    /// </summary>
    private async Task ReconcileOrphanReadyFichesForDraftAsync(
        SupervisorCellulePrimeDraftEntity draft,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (!IsDraftValidated(draft.Status)) return;

        var orphans = await db.EmployeePrimeServiceFiches
            .Where(f =>
                f.SupervisorUserId == draft.SupervisorUserId &&
                f.CelluleId == draft.CelluleId &&
                f.Period == draft.Period &&
                EF.Functions.ILike(f.FillingStatus, "complete") &&
                (f.ValidationStatus == PrimeValidationWorkflowService.AwaitingData ||
                 f.ValidationStatus == "NotStarted"))
            .ToListAsync(ct);

        foreach (var fiche in orphans)
        {
            fiche.CellulePrimeDraftId = draft.Id;
            await SyncValidationSubmissionStatusAsync(fiche, draft, now, ct);
        }
    }

    /// <summary>Résout le brouillon pôle validé pour une fiche (lien direct, cellule, ou pôle racine / période).</summary>
    public async Task<SupervisorCellulePrimeDraftEntity?> ResolveDraftForFicheAsync(
        EmployeePrimeServiceFicheEntity fiche,
        CancellationToken ct = default)
    {
        var candidates = await db.SupervisorCellulePrimeDrafts
            .Where(d =>
                d.SupervisorUserId == fiche.SupervisorUserId &&
                d.Period == fiche.Period &&
                EF.Functions.ILike(d.Status, "validated"))
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync(ct);

        if (candidates.Count == 0) return null;

        if (fiche.CellulePrimeDraftId != Guid.Empty)
        {
            var linked = candidates.FirstOrDefault(d => d.Id == fiche.CellulePrimeDraftId);
            if (linked is not null) return linked;
        }

        var ficheCell = fiche.CelluleId.Trim();
        var byCell = candidates.FirstOrDefault(d =>
            string.Equals(d.CelluleId, ficheCell, StringComparison.Ordinal));
        if (byCell is not null) return byCell;

        var poleId = await db.Cellules.AsNoTracking()
            .Where(c => c.Id == ficheCell)
            .Select(c => c.PoleId)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(poleId))
        {
            var poleTrim = poleId.Trim();
            var byPole = candidates.FirstOrDefault(d =>
                string.Equals(d.RootPoleId, poleTrim, StringComparison.Ordinal) ||
                string.Equals(d.CelluleId, poleTrim, StringComparison.Ordinal));
            if (byPole is not null) return byPole;
        }

        return candidates[0];
    }

    /// <summary>
    /// Passe en <see cref="PrimeValidationWorkflowService.Pending"/> toutes les fiches prêtes encore en attente de données.
    /// </summary>
    public async Task<int> ReconcileReadySubmissionsAsync(CancellationToken ct = default)
    {
        var fiches = await db.EmployeePrimeServiceFiches
            .Where(f =>
                (f.ValidationStatus == PrimeValidationWorkflowService.AwaitingData ||
                 f.ValidationStatus == "NotStarted") &&
                EF.Functions.ILike(f.FillingStatus, "complete"))
            .ToListAsync(ct);

        if (fiches.Count == 0) return 0;

        var changed = 0;
        var now = DateTimeOffset.UtcNow;
        foreach (var fiche in fiches)
        {
            var draft = await ResolveDraftForFicheAsync(fiche, ct);
            if (draft is null) continue;
            if (fiche.CellulePrimeDraftId != draft.Id)
                fiche.CellulePrimeDraftId = draft.Id;
            var before = fiche.ValidationStatus;
            await SyncValidationSubmissionStatusAsync(fiche, draft, now, ct);
            if (!string.Equals(before, fiche.ValidationStatus, StringComparison.Ordinal))
                changed++;
        }

        if (changed > 0)
            await db.SaveChangesAsync(ct);
        return changed;
    }

    public async Task<int> ReconcileReadySubmissionsForSupervisorPeriodAsync(
        string supervisorUserId,
        string period,
        CancellationToken ct = default)
    {
        var sup = supervisorUserId.Trim();
        var per = period.Trim();
        var fiches = await db.EmployeePrimeServiceFiches
            .Where(f =>
                f.SupervisorUserId == sup &&
                f.Period == per &&
                (f.ValidationStatus == PrimeValidationWorkflowService.AwaitingData ||
                 f.ValidationStatus == "NotStarted") &&
                EF.Functions.ILike(f.FillingStatus, "complete"))
            .ToListAsync(ct);

        if (fiches.Count == 0) return 0;

        var changed = 0;
        var now = DateTimeOffset.UtcNow;
        foreach (var fiche in fiches)
        {
            var draft = await ResolveDraftForFicheAsync(fiche, ct);
            if (draft is null) continue;
            if (fiche.CellulePrimeDraftId != draft.Id)
                fiche.CellulePrimeDraftId = draft.Id;
            var before = fiche.ValidationStatus;
            await SyncValidationSubmissionStatusAsync(fiche, draft, now, ct);
            if (!string.Equals(before, fiche.ValidationStatus, StringComparison.Ordinal))
                changed++;
        }

        if (changed > 0)
            await db.SaveChangesAsync(ct);
        return changed;
    }
}
