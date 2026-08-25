using Microsoft.EntityFrameworkCore;
using Prime.Application;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Services;

/// <summary>Accès lecture aperçu fusionné PRIME pour validateurs W1 et stakeholders W2.</summary>
public sealed class PrimeFicheMergedPreviewAccessService(
    PrimeDbContext db,
    PrimeRbacReadService rbac,
    PrimeOrgScopeService org,
    PrimeFicheValidationSubmissionService submission,
    GlobalPoolWorkflowService poolWf,
    ICommonLinePonderationResolver resolver)
{
    private const string MissingSnapshotHint =
        "Ré-enregistrez la partie commune depuis « Fiche PRIME — saisie » (ou réimportez l'Excel) pour activer l'aperçu et l'export recalculés.";

    public async Task<bool> CanAccessMergedPreviewAsync(
        PrimeResolvedUser ru,
        EmployeePrimeServiceFiche fiche,
        CancellationToken ct = default)
    {
        var role = ru.Role.Trim();
        var actor = PrimeRbacReadService.WithActingRole(ru.Employee, role);

        if (string.Equals(role, "Admin", StringComparison.Ordinal))
            return true;

        if (PrimeFicheDistributionAccess.RoleMustWaitForPrimeDistribution(role))
        {
            if (!string.Equals(fiche.EmployeeId, ru.UserId, StringComparison.Ordinal))
                return false;
            // Le pilote accède à sa fiche dès que SA ligne est validée par les deux workflows
            // (RH + Manager), indépendamment de l'avancement du reste du périmètre.
            return await FicheApprovedByBothWorkflowsAsync(fiche, ct);
        }

        if (string.Equals(role, PrimeFicheValidationRoles.Superviseur, StringComparison.Ordinal) &&
            await org.SupervisorOwnsCelluleAsync(ru.UserId, fiche.CelluleId, ct))
            return true;

        if (PrimeFicheValidationRoles.IsOperationalApprover(role))
        {
            if (await rbac.CanAccessFicheAsync(actor, fiche, "Read", ct)) return true;
            if (await rbac.CanAccessFicheAsync(actor, fiche, "Validate", ct)) return true;
            return false;
        }

        if (PrimeFicheValidationRoles.IsGlobalPoolStakeholder(role))
            return await FicheInGlobalPoolScopeAsync(fiche.Id, actor, ct);

        if (string.Equals(role, "Audit", StringComparison.Ordinal) &&
            await rbac.RoleHasActionAsync(role, "Read", ct))
        {
            var scopes = await rbac.GetAllowedScopesAsync(role, "Read", ct);
            if (scopes.Any(s => string.Equals(s.Trim(), "Global", StringComparison.Ordinal)))
                return true;
        }

        return false;
    }

    public async Task<MergedFichePreviewContextDto?> BuildContextAsync(
        EmployeePrimeServiceFiche fiche,
        CancellationToken ct = default)
    {
        SupervisorCellulePrimeDraft? draft = null;
        if (fiche.CellulePrimeDraftId != Guid.Empty)
        {
            draft = await db.SupervisorCellulePrimeDrafts.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == fiche.CellulePrimeDraftId, ct);
        }

        if (draft is null)
            draft = await submission.ResolveDraftForFicheAsync(fiche, ct);

        var emp = await db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == fiche.EmployeeId, ct);
        var display = emp is null ? fiche.EmployeeId : $"{emp.FirstName} {emp.LastName}".Trim();

        var templateId = (draft?.TemplateId ?? "").Trim();
        var unavailable = ResolvePreviewUnavailableReason(fiche, draft, templateId);

        var indicators = await db.ServicePrimeIndicators.AsNoTracking()
            .Where(x => x.ServiceId == fiche.ServiceId)
            .OrderBy(x => x.SortOrder)
            .Select(x => new ServicePrimeIndicatorDto
            {
                Id = x.Id,
                ServiceId = x.ServiceId,
                SortOrder = x.SortOrder,
                Label = x.Label,
                PonderationPrimePct = x.PonderationPrimePct,
                PonderationChallengePct = x.PonderationChallengePct,
                IsActive = x.IsActive,
                TemplateStableId = x.TemplateStableId,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
            })
            .ToListAsync(ct);

        var poleLinePonderations = await ResolvePoleLinePonderationsAsync(fiche, templateId, ct);

        return new MergedFichePreviewContextDto
        {
            FicheId = fiche.Id,
            EmployeeId = fiche.EmployeeId,
            EmployeeDisplayName = display,
            Period = fiche.Period,
            TemplateId = templateId,
            SchemaJson = draft?.SchemaJson ?? "{}",
            PoleSaisieJson = draft?.CelluleSaisieJson ?? "{}",
            CellSaisieJson = fiche.ServiceSaisieJson ?? "{}",
            TemplateCalcSnapshotJson = draft?.TemplateCalcSnapshotJson,
            Indicators = indicators,
            PoleLinePonderations = poleLinePonderations,
            PreviewAvailable = unavailable is null,
            PreviewUnavailableReason = unavailable,
        };
    }

    private static string? ResolvePreviewUnavailableReason(
        EmployeePrimeServiceFiche fiche,
        SupervisorCellulePrimeDraft? draft,
        string templateId)
    {
        if (!PrimeFicheValidationSubmissionService.IsCellPartComplete(fiche.FillingStatus))
            return "Fiche pilote non complète.";
        if (draft is null)
            return "Enregistrez la fiche cellule et le brouillon pôle.";
        if (string.IsNullOrWhiteSpace(templateId))
            return "Créez la partie commune (template) pour cette période.";
        if (string.IsNullOrWhiteSpace(draft.TemplateCalcSnapshotJson) ||
            string.Equals(draft.TemplateCalcSnapshotJson.Trim(), "{}", StringComparison.Ordinal) ||
            string.Equals(draft.TemplateCalcSnapshotJson.Trim(), "null", StringComparison.OrdinalIgnoreCase))
            return MissingSnapshotHint;
        return null;
    }

    private async Task<List<ServicePoleLinePonderationDto>> ResolvePoleLinePonderationsAsync(
        EmployeePrimeServiceFiche fiche,
        string templateId,
        CancellationToken ct)
    {
        var frozen = CommonLinePonderationResolver.TryParseSnapshot(fiche.PonderationsSnapshotJson);
        if (frozen is not null)
            return CommonLinePonderationPeriod.ToPoleLineDtos(fiche.ServiceId, frozen);

        var at = CommonLinePonderationPeriod.ForLiveResolve();
        var resolved = await resolver.ResolveAsync(
            fiche.ServiceId,
            fiche.CelluleId,
            templateId,
            at,
            templateLines: null,
            previousPeriodLines: null,
            ct);
        return CommonLinePonderationPeriod.ToPoleLineDtos(fiche.ServiceId, resolved);
    }

    private async Task<bool> FicheInGlobalPoolScopeAsync(
        Guid ficheId,
        Employee actor,
        CancellationToken ct)
    {
        var inSynthesis = await db.GlobalPoolSynthesisLines.AsNoTracking()
            .AnyAsync(l => l.FicheId == ficheId, ct);
        if (!inSynthesis) return false;

        if (!await rbac.RoleHasActionAsync(actor.Role, "Read", ct))
            return false;

        var scopes = await rbac.GetAllowedScopesAsync(actor.Role, "Read", ct);
        return scopes.Any(s => string.Equals(s.Trim(), "Global", StringComparison.Ordinal));
    }

    /// <summary>Vrai si la ligne de synthèse (périmètre le plus récent) de cette fiche est validée
    /// par les deux workflows (RH + Manager), c.-à-d. LineStatus = Approved.</summary>
    public async Task<bool> FicheApprovedByBothWorkflowsAsync(
        EmployeePrimeServiceFiche fiche,
        CancellationToken ct)
    {
        // Tri côté client : SQLite (tests) ne sait pas trier sur DateTimeOffset.
        var candidates = await db.GlobalPoolSynthesisLines.AsNoTracking()
            .Where(l => l.FicheId == fiche.Id)
            .Select(l => new { l.LineStatus, l.ScopeSynthesis.UpdatedAt })
            .ToListAsync(ct);
        var status = candidates
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => x.LineStatus)
            .FirstOrDefault();
        return string.Equals(status, GlobalPoolSynthesisLineStatuses.Approved, StringComparison.Ordinal);
    }

    private async Task<bool> PoolDistributionUnlockedForFicheAsync(
        EmployeePrimeServiceFiche fiche,
        CancellationToken ct)
    {
        var draft = await db.SupervisorCellulePrimeDrafts.AsNoTracking()
            .Where(d => d.Period == fiche.Period && d.CelluleId == fiche.CelluleId)
            .OrderByDescending(d => d.UpdatedAt)
            .FirstOrDefaultAsync(ct);
        if (draft is null) return false;
        return await poolWf.PoolDistributionUnlockedAsync(draft, ct);
    }
}
