using Microsoft.EntityFrameworkCore;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Services;

/// <summary>Import d'une fiche PRIME déjà prête (Excel/CSV) — mode workflow ou historique.</summary>
public sealed class PrimeFicheImportService(
    PrimeDbContext db,
    PrimeOrgScopeService org,
    PrimeFicheValidationSubmissionService submission,
    PrimeValidationWorkflowRuntime wfRuntime,
    ICommonLinePonderationResolver ponderations)
{
    public const string ImportTemplateId = "fiche-import";
    public const string ImportTemplateDisplayName = "Import fiche prête";

    public async Task<(bool ok, string? error, ImportReadyFicheResponseDto? result)> ImportReadyFicheAsync(
        ImportReadyFicheRequest request,
        CancellationToken ct = default)
    {
        var sup = (request.SupervisorUserId ?? "").Trim();
        var period = (request.Period ?? "").Trim();
        var rawCellule = (request.CelluleId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(sup) || string.IsNullOrWhiteSpace(period) || string.IsNullOrWhiteSpace(rawCellule))
            return (false, "supervisorUserId, period et celluleId sont requis.", null);

        if (!PrimePeriodRules.IsClosedPeriod(period))
            return (false, PrimePeriodRules.ClosedPeriodRequiredMessage(period), null);

        if (request.Rows is null || request.Rows.Count == 0)
            return (false, "La grille importée est vide.", null);

        var celluleCanon = await org.NormalizeSupervisorDraftCelluleKeyAsync(sup, rawCellule, ct);
        if (celluleCanon is null)
            return (false, "Accès refusé pour ce périmètre.", null);

        if (!await org.SupervisorOwnsCelluleAsync(sup, celluleCanon, ct))
            return (false, "Accès refusé pour ce périmètre.", null);

        var rootPoleId = await org.ResolveRootPoleIdForCelluleAsync(celluleCanon, ct);
        if (string.IsNullOrWhiteSpace(rootPoleId))
            return (false, "Impossible de résoudre le pôle racine pour cette cellule.", null);

        var now = DateTimeOffset.UtcNow;
        var snap = BuildSnapshot(request);

        if (request.IsHistorical)
            return await ImportHistoricalAsync(request, sup, period, celluleCanon, rootPoleId, snap, now, ct);

        return await ImportWorkflowAsync(request, sup, period, celluleCanon, rootPoleId, snap, now, ct);
    }

    public async Task<List<PrimeHistoricalFicheListItemDto>> ListHistoricalAsync(
        string supervisorUserId,
        string? period,
        string? role,
        CancellationToken ct = default)
    {
        var sup = supervisorUserId.Trim();
        var isAdmin = string.Equals((role ?? "").Trim(), "Admin", StringComparison.Ordinal);

        IQueryable<PrimeHistoricalFicheEntity> q = db.PrimeHistoricalFiches.AsNoTracking();
        if (!isAdmin)
        {
            var celluleIds = await org.GetSupervisedCelluleIdsAsync(sup, ct);
            if (celluleIds.Count == 0) return [];
            q = q.Where(h => h.SupervisorUserId == sup && celluleIds.Contains(h.CelluleId));
        }

        if (!string.IsNullOrWhiteSpace(period))
            q = q.Where(h => h.Period == period.Trim());

        return await q
            .OrderByDescending(h => h.ImportedAt)
            .Select(h => new PrimeHistoricalFicheListItemDto
            {
                Id = h.Id,
                Period = h.Period,
                CelluleId = h.CelluleId,
                ServiceId = h.ServiceId,
                EmployeeExternalName = h.EmployeeExternalName,
                EmployeeId = h.EmployeeId,
                PrimeAmount = h.PrimeAmount,
                ChallengeAmount = h.ChallengeAmount,
                TotalAmount = h.TotalAmount,
                OriginFileName = h.OriginFileName,
                Source = h.Source,
                ImportedAt = h.ImportedAt,
                HasDetailGrid = h.DetailGridJson != null && h.DetailGridJson != "",
            })
            .ToListAsync(ct);
    }

    public async Task<(bool ok, string? error, PrimeHistoricalFicheDetailSnapshotDto? result)> GetHistoricalDetailSnapshotAsync(
        Guid historicalFicheId,
        string supervisorUserId,
        string? role,
        CancellationToken ct = default)
    {
        var sup = supervisorUserId.Trim();
        var isAdmin = string.Equals((role ?? "").Trim(), "Admin", StringComparison.Ordinal);

        var entity = await db.PrimeHistoricalFiches.AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == historicalFicheId, ct);
        if (entity is null)
            return (false, "Archive introuvable.", null);

        if (!isAdmin)
        {
            var celluleIds = await org.GetSupervisedCelluleIdsAsync(sup, ct);
            if (entity.SupervisorUserId != sup || !celluleIds.Contains(entity.CelluleId))
                return (false, "Accès refusé pour cette archive.", null);
        }

        var snap = PrimeFicheDetailSnapshotService.TryParseSnapshot(entity.DetailGridJson);
        if (snap is null)
            return (false, "Grille détaillée indisponible pour cette archive.", null);

        return (true, null, new PrimeHistoricalFicheDetailSnapshotDto
        {
            HistoricalFicheId = entity.Id,
            Version = snap.Version,
            PreviewSheetName = snap.PreviewSheetName,
            TemplateVersionRef = snap.TemplateVersionRef,
            Rows = snap.Rows,
            Errors = snap.Errors,
            PrimeAmount = snap.PrimeAmount ?? entity.PrimeAmount,
            ChallengeAmount = snap.ChallengeAmount ?? entity.ChallengeAmount,
            TotalAmount = snap.TotalAmount ?? entity.TotalAmount,
            EmployeeExternalName = entity.EmployeeExternalName,
            Period = entity.Period,
            OriginFileName = entity.OriginFileName,
            ImportedAt = entity.ImportedAt,
        });
    }

    private async Task<(bool ok, string? error, ImportReadyFicheResponseDto? result)> ImportWorkflowAsync(
        ImportReadyFicheRequest request,
        string sup,
        string period,
        string celluleCanon,
        string rootPoleId,
        PrimeFicheDetailSnapshotService.DetailSnapshotV1 snap,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var employeeId = (request.EmployeeId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(employeeId))
            return (false, "employeeId est requis pour un import en mode workflow.", null);

        var emp = await org.GetEmployeeAsync(employeeId, ct);
        if (emp is null)
            return (false, "Employé introuvable.", null);
        if (!string.Equals(emp.CelluleId, celluleCanon, StringComparison.Ordinal))
            return (false, "L'employé n'appartient pas à la cellule sélectionnée.", null);

        var draft = await EnsureImportDraftAsync(sup, rootPoleId, celluleCanon, period, validated: true, now, ct);
        var existing = await db.EmployeePrimeServiceFiches
            .FirstOrDefaultAsync(f => f.EmployeeId == emp.Id && f.Period == period, ct);

        if (existing is not null && await wfRuntime.IsTerminalStatusAsync(existing.ValidationStatus, ct))
            return (false, "Une fiche validée existe déjà pour cet employé et cette période — import impossible.", null);

        EmployeePrimeServiceFiche fiche;
        if (existing is null)
        {
            fiche = new EmployeePrimeServiceFiche
            {
                Id = Guid.NewGuid(),
                CellulePrimeDraftId = draft.Id,
                SupervisorUserId = sup,
                EmployeeId = emp.Id,
                ServiceId = emp.ServiceId ?? "",
                CelluleId = emp.CelluleId ?? celluleCanon,
                Period = period,
                ServiceSaisieJson = NormalizeSaisieJson(request.ServiceSaisieJson),
                FillingStatus = "Complete",
                ValidationStatus = PrimeValidationWorkflowService.AwaitingData,
                UpdatedAt = now,
            };
            db.EmployeePrimeServiceFiches.Add(fiche);
        }
        else
        {
            fiche = existing;
            PrepareFicheForImportReplace(fiche);
            fiche.CellulePrimeDraftId = draft.Id;
            fiche.SupervisorUserId = sup;
            fiche.ServiceId = emp.ServiceId ?? fiche.ServiceId;
            fiche.CelluleId = emp.CelluleId ?? celluleCanon;
            fiche.ServiceSaisieJson = NormalizeSaisieJson(request.ServiceSaisieJson);
            fiche.FillingStatus = "Complete";
            fiche.ValidationStatus = PrimeValidationWorkflowService.AwaitingData;
            fiche.UpdatedAt = now;
        }

        if (!PrimeFicheDetailSnapshotService.TryApplySnapshot(fiche, snap, freeze: true, now, out var snapErr))
            return (false, snapErr, null);

        await ponderations.FreezeOntoFicheIfMissingAsync(fiche, draft.TemplateId, ct);

        await submission.SyncValidationSubmissionStatusAsync(fiche, draft, now, ct);

        await db.SaveChangesAsync(ct);

        return (true, null, new ImportReadyFicheResponseDto
        {
            Outcome = "WorkflowFiche",
            FicheId = fiche.Id,
            EmployeeId = emp.Id,
            EmployeeDisplayName = $"{emp.FirstName} {emp.LastName}".Trim(),
            Period = period,
            ValidationStatus = fiche.ValidationStatus,
            ImportedAt = now,
        });
    }

    private async Task<(bool ok, string? error, ImportReadyFicheResponseDto? result)> ImportHistoricalAsync(
        ImportReadyFicheRequest request,
        string sup,
        string period,
        string celluleCanon,
        string rootPoleId,
        PrimeFicheDetailSnapshotService.DetailSnapshotV1 snap,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var employeeId = (request.EmployeeId ?? "").Trim();
        EmployeeEntity? emp = null;
        if (!string.IsNullOrWhiteSpace(employeeId))
            emp = await org.GetEmployeeAsync(employeeId, ct);

        if (emp is not null)
        {
            var draft = await EnsureImportDraftAsync(sup, rootPoleId, celluleCanon, period, validated: true, now, ct);
            var existing = await db.EmployeePrimeServiceFiches
                .FirstOrDefaultAsync(f => f.EmployeeId == emp.Id && f.Period == period, ct);

            if (existing is not null && await wfRuntime.IsTerminalStatusAsync(existing.ValidationStatus, ct)
                && !PrimeValidationWorkflowService.IsHistoricalImport(existing.ValidationStatus))
                return (false, "Une fiche validée existe déjà pour cet employé et cette période.", null);

            EmployeePrimeServiceFiche fiche;
            if (existing is null)
            {
                fiche = new EmployeePrimeServiceFiche
                {
                    Id = Guid.NewGuid(),
                    CellulePrimeDraftId = draft.Id,
                    SupervisorUserId = sup,
                    EmployeeId = emp.Id,
                    ServiceId = emp.ServiceId ?? "",
                    CelluleId = emp.CelluleId ?? celluleCanon,
                    Period = period,
                    ServiceSaisieJson = NormalizeSaisieJson(request.ServiceSaisieJson),
                    FillingStatus = "Complete",
                    ValidationStatus = PrimeValidationWorkflowService.HistoricalImport,
                    UpdatedAt = now,
                };
                db.EmployeePrimeServiceFiches.Add(fiche);
            }
            else
            {
                fiche = existing;
                PrepareFicheForImportReplace(fiche);
                fiche.CellulePrimeDraftId = draft.Id;
                fiche.ServiceSaisieJson = NormalizeSaisieJson(request.ServiceSaisieJson);
                fiche.FillingStatus = "Complete";
                fiche.ValidationStatus = PrimeValidationWorkflowService.HistoricalImport;
                fiche.UpdatedAt = now;
            }

            if (!PrimeFicheDetailSnapshotService.TryApplySnapshot(fiche, snap, freeze: true, now, out var snapErr))
                return (false, snapErr, null);

            await ponderations.FreezeOntoFicheIfMissingAsync(fiche, draft.TemplateId, ct);

            await db.SaveChangesAsync(ct);
            return (true, null, new ImportReadyFicheResponseDto
            {
                Outcome = "HistoricalLinkedFiche",
                FicheId = fiche.Id,
                EmployeeId = emp.Id,
                EmployeeDisplayName = $"{emp.FirstName} {emp.LastName}".Trim(),
                Period = period,
                ValidationStatus = fiche.ValidationStatus,
                ImportedAt = now,
            });
        }

        var externalName = (request.EmployeeExternalName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(externalName))
            externalName = employeeId.Length > 0 ? employeeId : "Employé inconnu";

        var archive = new PrimeHistoricalFicheEntity
        {
            Id = Guid.NewGuid(),
            Period = period,
            CelluleId = celluleCanon,
            ServiceId = null,
            RootPoleId = rootPoleId,
            SupervisorUserId = sup,
            EmployeeExternalName = externalName,
            EmployeeId = string.IsNullOrWhiteSpace(employeeId) ? null : employeeId,
            DetailGridJson = PrimeFicheDetailSnapshotService.SerializeSnapshot(snap),
            DetailGridPreviewSheetName = snap.PreviewSheetName,
            PrimeAmount = snap.PrimeAmount,
            ChallengeAmount = snap.ChallengeAmount,
            TotalAmount = snap.TotalAmount,
            ServiceSaisieJson = NormalizeSaisieJson(request.ServiceSaisieJson),
            OriginFileName = (request.OriginFileName ?? "").Trim(),
            Source = "Import",
            ImportedByUserId = sup,
            ImportedAt = now,
        };
        db.PrimeHistoricalFiches.Add(archive);
        await db.SaveChangesAsync(ct);

        return (true, null, new ImportReadyFicheResponseDto
        {
            Outcome = "HistoricalArchive",
            HistoricalFicheId = archive.Id,
            EmployeeDisplayName = externalName,
            Period = period,
            ValidationStatus = PrimeValidationWorkflowService.HistoricalImport,
            ImportedAt = now,
        });
    }

    private async Task<SupervisorCellulePrimeDraft> EnsureImportDraftAsync(
        string sup,
        string rootPoleId,
        string celluleId,
        string period,
        bool validated,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var draft = await db.SupervisorCellulePrimeDrafts
            .FirstOrDefaultAsync(d => d.SupervisorUserId == sup && d.RootPoleId == rootPoleId && d.Period == period, ct);

        if (draft is not null)
        {
            if (validated && !PrimeFicheValidationSubmissionService.IsDraftValidated(draft.Status))
            {
                draft.Status = "Validated";
                draft.UpdatedAt = now;
            }
            return draft;
        }

        draft = new SupervisorCellulePrimeDraft
        {
            Id = Guid.NewGuid(),
            SupervisorUserId = sup,
            RootPoleId = rootPoleId,
            CelluleId = celluleId,
            Period = period,
            TemplateId = ImportTemplateId,
            TemplateDisplayName = ImportTemplateDisplayName,
            TemplateFormatVersion = 1,
            Status = validated ? "Validated" : "Draft",
            SchemaJson = "{}",
            CelluleSaisieJson = "{}",
            UpdatedAt = now,
        };
        db.SupervisorCellulePrimeDrafts.Add(draft);
        await db.SaveChangesAsync(ct);
        return draft;
    }

    private static PrimeFicheDetailSnapshotService.DetailSnapshotV1 BuildSnapshot(ImportReadyFicheRequest request) =>
        new()
        {
            PreviewSheetName = request.PreviewSheetName,
            TemplateVersionRef = request.TemplateVersionRef ?? $"{ImportTemplateId}:v1",
            Rows = request.Rows ?? [],
            Errors = request.Errors ?? [],
            ComputedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimeAmount = request.PrimeAmount,
            ChallengeAmount = request.ChallengeAmount,
            TotalAmount = request.TotalAmount,
        };

    private static void PrepareFicheForImportReplace(EmployeePrimeServiceFiche fiche)
    {
        if (PrimeFicheDetailSnapshotService.IsFrozen(fiche))
            fiche.DetailGridFrozenAt = null;
        fiche.PonderationsSnapshotJson = null;
    }

    private static string NormalizeSaisieJson(string? json)
    {
        var t = (json ?? "").Trim();
        return t.Length > 0 ? t : "{}";
    }
}
