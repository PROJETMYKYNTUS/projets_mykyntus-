using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;

namespace PrimeBackend.Services;

/// <summary>Règles centralisées de suppression PRIME — évite la perte d'historique validé ou figé.</summary>
public sealed class PrimeDeletionGuardService(PrimeDbContext db, PrimeValidationWorkflowRuntime wfRuntime)
{
    private enum PilotBlockKind
    {
        None,
        FrozenSnapshot,
        HistoricalImport,
        TerminalStatus,
        InWorkflow,
    }

    public static bool CanHardDeleteTemplate(PrimeFicheTemplateUsageDto usage) =>
        usage.TotalReferenceCount == 0 && usage.FrozenPilotFicheCount == 0;

    public static string RecommendedTemplateAction(PrimeFicheTemplateUsageDto usage) =>
        CanHardDeleteTemplate(usage) ? "hardDelete" : "archive";

    public async Task<(bool canDelete, string? reason, DeletionImpactDto impact)> CanDeleteCommonsDraftAsync(
        Guid draftId,
        CancellationToken ct = default)
    {
        var draft = await db.SupervisorCellulePrimeDrafts.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == draftId, ct);
        if (draft is null)
            return (false, "Fiche commune introuvable.", new DeletionImpactDto());

        if (HasGlobalPoolActivity(draft))
        {
            return (false,
                "Suppression impossible : un fichier pool global a été déposé ou validé pour cette fiche commune.",
                new DeletionImpactDto { HasGlobalPool = true });
        }

        var pilots = await db.EmployeePrimeServiceFiches.AsNoTracking()
            .Where(f => f.CellulePrimeDraftId == draftId)
            .ToListAsync(ct);

        return await EvaluatePilotCollectionAsync(pilots, ct);
    }

    public async Task<(bool canDelete, string? reason)> CanDeletePilotFicheAsync(
        Guid ficheId,
        CancellationToken ct = default)
    {
        var fiche = await db.EmployeePrimeServiceFiches.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == ficheId, ct);
        if (fiche is null) return (false, "Fiche pilote introuvable.");

        var kind = await EvaluatePilotBlockKindAsync(fiche, ct);
        return kind == PilotBlockKind.None
            ? (true, null)
            : (false, BlockReasonForKind(kind));
    }

    /// <summary>Indicateur référencé dans une fiche pilote non supprimable du service.</summary>
    public async Task<bool> IsIndicatorProtectedByFichesAsync(
        ServicePrimeIndicatorEntity indicator,
        string serviceId,
        CancellationToken ct = default)
    {
        var cid = serviceId.Trim();
        var fiches = await db.EmployeePrimeServiceFiches.AsNoTracking()
            .Where(f => f.ServiceId == cid)
            .ToListAsync(ct);

        foreach (var fiche in fiches)
        {
            var kind = await EvaluatePilotBlockKindAsync(fiche, ct);
            if (kind == PilotBlockKind.None) continue;
            if (IsIndicatorReferencedInJson(indicator, fiche.ServiceSaisieJson))
                return true;
        }

        return false;
    }

    public static bool IsIndicatorReferencedInJson(ServicePrimeIndicatorEntity indicator, string? serviceSaisieJson)
    {
        var json = (serviceSaisieJson ?? "").Trim();
        if (json.Length == 0) return false;

        var stable = (indicator.TemplateStableId ?? "").Trim();
        if (stable.Length > 0 &&
            json.Contains(stable, StringComparison.OrdinalIgnoreCase))
            return true;

        var label = indicator.Label.Trim();
        return label.Length > 2 &&
               json.Contains(label, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(bool canDelete, string? reason, DeletionImpactDto impact)> EvaluatePilotCollectionAsync(
        IReadOnlyList<EmployeePrimeServiceFicheEntity> pilots,
        CancellationToken ct)
    {
        var frozen = 0;
        var inWorkflow = 0;
        var terminal = 0;
        var deletable = 0;

        foreach (var pilot in pilots)
        {
            var kind = await EvaluatePilotBlockKindAsync(pilot, ct);
            switch (kind)
            {
                case PilotBlockKind.FrozenSnapshot:
                    frozen++;
                    break;
                case PilotBlockKind.HistoricalImport:
                case PilotBlockKind.TerminalStatus:
                    terminal++;
                    break;
                case PilotBlockKind.InWorkflow:
                    inWorkflow++;
                    break;
                default:
                    deletable++;
                    break;
            }
        }

        var blocked = pilots.Count - deletable;
        var impact = new DeletionImpactDto
        {
            TotalPilotCount = pilots.Count,
            DeletablePilotCount = deletable,
            BlockedPilotCount = blocked,
            FrozenCount = frozen,
            InWorkflowCount = inWorkflow,
            TerminalCount = terminal,
        };

        if (blocked == 0)
            return (true, null, impact);

        if (frozen > 0)
        {
            return (false,
                "Suppression impossible : des fiches pilotes ont un historique figé (snapshot). Le template et la grille restent conservés sur ces fiches.",
                impact);
        }

        if (terminal > 0)
        {
            return (false,
                "Suppression impossible : des fiches pilotes sont en statut terminal (validées, rejetées ou import historique).",
                impact);
        }

        return (false,
            "Suppression impossible : des fiches pilotes sont entrées dans le circuit de validation.",
            impact);
    }

    private async Task<PilotBlockKind> EvaluatePilotBlockKindAsync(
        EmployeePrimeServiceFicheEntity fiche,
        CancellationToken ct)
    {
        if (fiche.DetailGridFrozenAt.HasValue)
            return PilotBlockKind.FrozenSnapshot;

        if (PrimeValidationWorkflowService.IsHistoricalImport(fiche.ValidationStatus))
            return PilotBlockKind.HistoricalImport;

        if (await wfRuntime.IsTerminalStatusAsync(fiche.ValidationStatus, ct))
            return PilotBlockKind.TerminalStatus;

        if (!PrimeValidationWorkflowService.IsPreWorkflowStatus(fiche.ValidationStatus))
            return PilotBlockKind.InWorkflow;

        return PilotBlockKind.None;
    }

    private static bool HasGlobalPoolActivity(SupervisorCellulePrimeDraftEntity draft) =>
        draft.GlobalPoolExcelContent is { Length: > 0 } ||
        draft.GlobalPoolUploadedAt.HasValue ||
        draft.GlobalPoolManagerApprovedAt.HasValue ||
        draft.GlobalPoolRhApprovedAt.HasValue ||
        draft.GlobalPoolComptaAckAt.HasValue;

    private static string BlockReasonForKind(PilotBlockKind kind) => kind switch
    {
        PilotBlockKind.FrozenSnapshot =>
            "Suppression impossible : la fiche a un historique figé (snapshot).",
        PilotBlockKind.HistoricalImport =>
            "Suppression impossible : fiche import historique.",
        PilotBlockKind.TerminalStatus =>
            "Suppression impossible : fiche en statut terminal (validée ou rejetée).",
        PilotBlockKind.InWorkflow =>
            "Suppression impossible : fiche entrée dans le circuit de validation.",
        _ => "Suppression impossible.",
    };
}
