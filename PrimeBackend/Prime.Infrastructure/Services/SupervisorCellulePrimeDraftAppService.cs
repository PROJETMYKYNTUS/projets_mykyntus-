using Microsoft.EntityFrameworkCore;
using Prime.Application;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Infrastructure.Persistence;
using Prime.Infrastructure.Services;

namespace Prime.Infrastructure.Services;

public sealed class SupervisorCellulePrimeDraftAppService(
    PrimeDbContext db,
    PrimeOrgScopeService org,
    PrimeFicheValidationSubmissionService submission) : ISupervisorCellulePrimeDraftAppService
{
    private static SupervisorCellulePrimeDraftResponseDto Map(SupervisorCellulePrimeDraft e) =>
        new()
        {
            Id = e.Id,
            SupervisorUserId = e.SupervisorUserId,
            CelluleId = e.CelluleId,
            Period = e.Period,
            TemplateId = e.TemplateId,
            TemplateDisplayName = e.TemplateDisplayName,
            TemplateFormatVersion = e.TemplateFormatVersion,
            Status = e.Status,
            SchemaJson = e.SchemaJson,
            CelluleSaisieJson = e.CelluleSaisieJson,
            ComputedJson = e.ComputedJson,
            TemplateCalcSnapshotJson = e.TemplateCalcSnapshotJson,
            UpdatedAt = e.UpdatedAt,
        };

    public async Task<SupervisorCellulePrimeDraftResponseDto?> GetAsync(
        string supervisorUserId,
        string? celluleId,
        string? poleId,
        string period,
        string templateId,
        CancellationToken ct = default)
    {
        var rawKey = !string.IsNullOrWhiteSpace(celluleId) ? celluleId.Trim() : (poleId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(supervisorUserId) || string.IsNullOrWhiteSpace(rawKey) ||
            string.IsNullOrWhiteSpace(period) || string.IsNullOrWhiteSpace(templateId))
            throw new ArgumentException("supervisorUserId, celluleId (ou poleId), period et templateId sont requis.");

        var celluleCanon = await org.NormalizeSupervisorDraftCelluleKeyAsync(supervisorUserId, rawKey, ct)
            ?? throw new UnauthorizedAccessException("Accès refusé pour ce périmètre.");

        var entity = await db.SupervisorCellulePrimeDrafts.AsNoTracking().FirstOrDefaultAsync(
            x => x.SupervisorUserId == supervisorUserId.Trim() && x.CelluleId == celluleCanon &&
                 x.Period == period.Trim() && x.TemplateId == templateId.Trim(), ct);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<SupervisorCellulePrimeDraftListItemDto>> ListActiveAsync(
        string supervisorUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(supervisorUserId))
            throw new ArgumentException("supervisorUserId est requis.");

        var supTrim = supervisorUserId.Trim();
        var celluleIds = await org.GetSupervisedCelluleIdsAsync(supTrim, ct);
        if (celluleIds.Count == 0) return [];

        var drafts = await db.SupervisorCellulePrimeDrafts.AsNoTracking()
            .Where(d => d.SupervisorUserId == supTrim && celluleIds.Contains(d.CelluleId))
            .ToListAsync(ct);
        if (drafts.Count == 0) return [];

        var draftIds = drafts.Select(d => d.Id).ToList();
        var fiches = await db.EmployeePrimeServiceFiches.AsNoTracking()
            .Where(f => draftIds.Contains(f.CellulePrimeDraftId))
            .ToListAsync(ct);
        var fichesByDraft = fiches
            .GroupBy(f => f.CellulePrimeDraftId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var employeesByPole = await org.GetEmployeeCountsByCelluleAsync(celluleIds, ct);

        var result = new List<SupervisorCellulePrimeDraftListItemDto>();
        foreach (var draft in drafts
                     .GroupBy(d => new { d.RootPoleId, d.Period })
                     .Select(g => g.OrderByDescending(x => x.UpdatedAt).First()))
        {
            var total = employeesByPole.TryGetValue(draft.CelluleId, out var t) ? t : 0;
            fichesByDraft.TryGetValue(draft.Id, out var draftFiches);

            var complete = 0;
            var inProgress = 0;
            if (draftFiches != null)
            {
                foreach (var f in draftFiches)
                {
                    if (string.Equals(f.FillingStatus, "Complete", StringComparison.OrdinalIgnoreCase)) complete++;
                    else if (string.Equals(f.FillingStatus, "InProgress", StringComparison.OrdinalIgnoreCase)) inProgress++;
                }
            }
            var notStarted = Math.Max(0, total - complete - inProgress);

            var isValidated = string.Equals(draft.Status, "Validated", StringComparison.OrdinalIgnoreCase);
            var isFullyComplete = isValidated && total > 0 && complete == total;
            if (isFullyComplete) continue;

            result.Add(new SupervisorCellulePrimeDraftListItemDto
            {
                Id = draft.Id,
                SupervisorUserId = draft.SupervisorUserId,
                CelluleId = draft.CelluleId,
                Period = draft.Period,
                TemplateId = draft.TemplateId,
                TemplateDisplayName = draft.TemplateDisplayName,
                TemplateFormatVersion = draft.TemplateFormatVersion,
                Status = draft.Status,
                TotalEmployees = total,
                CompleteEmployees = complete,
                InProgressEmployees = inProgress,
                NotStartedEmployees = notStarted,
                IsFullyComplete = false,
                UpdatedAt = draft.UpdatedAt,
                HasGlobalPoolFile = draft.GlobalPoolExcelContent is { Length: > 0 },
                PoolDistributionUnlocked = draft.GlobalPoolManagerApprovedAt.HasValue && draft.GlobalPoolRhApprovedAt.HasValue,
            });
        }

        return result
            .OrderByDescending(r => r.Period)
            .ThenByDescending(r => r.UpdatedAt)
            .ToList();
    }

    public async Task<SupervisorCellulePrimeDraftResponseDto> UpsertAsync(
        UpsertSupervisorCellulePrimeDraftRequest body,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(body.CelluleId) && !string.IsNullOrWhiteSpace(body.PoleId))
            body.CelluleId = body.PoleId.Trim();
        if (!string.IsNullOrWhiteSpace(body.PoleSaisieJson) &&
            (string.IsNullOrWhiteSpace(body.CelluleSaisieJson) || string.Equals(body.CelluleSaisieJson.Trim(), "{}", StringComparison.Ordinal)))
            body.CelluleSaisieJson = body.PoleSaisieJson.Trim();

        if (string.IsNullOrWhiteSpace(body.SupervisorUserId) || string.IsNullOrWhiteSpace(body.CelluleId) ||
            string.IsNullOrWhiteSpace(body.Period) || string.IsNullOrWhiteSpace(body.TemplateId))
            throw new ArgumentException("Champs obligatoires manquants.");

        var celluleCanon = await org.NormalizeSupervisorDraftCelluleKeyAsync(body.SupervisorUserId, body.CelluleId, ct)
            ?? throw new UnauthorizedAccessException(
                "Accès refusé pour ce périmètre (identifiant cellule / pôle non reconnu pour ce superviseur).");
        body.CelluleId = celluleCanon;

        var poleSaisieToStore = CelluleDraftPayloadNormalizer.NormalizeCelluleSaisieJson(body.SchemaJson, body.CelluleSaisieJson);

        var supTrim = body.SupervisorUserId.Trim();
        var poleTrim = body.CelluleId.Trim();
        var periodTrim = body.Period.Trim();
        var templateTrim = body.TemplateId.Trim();

        var rootPoleId = await org.ResolveRootPoleIdForCelluleAsync(poleTrim, ct);
        if (string.IsNullOrWhiteSpace(rootPoleId))
            throw new ArgumentException(
                "Impossible de résoudre le pôle racine pour cette cellule. Vérifiez la structure RH (prime_pole / prime_cellule).");

        var now = DateTimeOffset.UtcNow;
        var entity = await db.SupervisorCellulePrimeDrafts.FirstOrDefaultAsync(
            x => x.SupervisorUserId == supTrim && x.RootPoleId == rootPoleId && x.Period == periodTrim, ct);

        if (entity is null)
        {
            var stale = await db.SupervisorCellulePrimeDrafts
                .Where(x => x.SupervisorUserId == supTrim && x.RootPoleId == rootPoleId && x.Period == periodTrim)
                .ToListAsync(ct);
            if (stale.Count > 0) db.SupervisorCellulePrimeDrafts.RemoveRange(stale);

            entity = new SupervisorCellulePrimeDraft
            {
                Id = Guid.NewGuid(),
                SupervisorUserId = supTrim,
                RootPoleId = rootPoleId,
                CelluleId = poleTrim,
                Period = periodTrim,
                TemplateId = templateTrim,
                TemplateDisplayName = (body.TemplateDisplayName ?? "").Trim(),
                TemplateFormatVersion = body.TemplateFormatVersion,
                Status = string.IsNullOrWhiteSpace(body.Status) ? "Draft" : body.Status!.Trim(),
                SchemaJson = body.SchemaJson,
                CelluleSaisieJson = poleSaisieToStore,
                ComputedJson = body.ComputedJson,
                TemplateCalcSnapshotJson = body.TemplateCalcSnapshotJson,
                UpdatedAt = now,
            };
            db.SupervisorCellulePrimeDrafts.Add(entity);
        }
        else
        {
            entity.RootPoleId = rootPoleId;
            entity.CelluleId = poleTrim;
            entity.TemplateId = templateTrim;
            entity.TemplateDisplayName = (body.TemplateDisplayName ?? "").Trim();
            entity.TemplateFormatVersion = body.TemplateFormatVersion;
            if (!string.IsNullOrWhiteSpace(body.Status)) entity.Status = body.Status.Trim();
            entity.SchemaJson = body.SchemaJson;
            entity.CelluleSaisieJson = poleSaisieToStore;
            entity.ComputedJson = body.ComputedJson;
            entity.TemplateCalcSnapshotJson = body.TemplateCalcSnapshotJson;
            entity.UpdatedAt = now;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            throw new PrimeApiException(409, DbExceptionMessages.FromSaveChanges(ex));
        }

        await submission.SyncForDraftAsync(entity.Id, ct);
        await db.SaveChangesAsync(ct);

        return Map(entity);
    }

    public async Task DeleteAsync(Guid id, string supervisorUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(supervisorUserId))
            throw new ArgumentException("supervisorUserId est requis.");

        var entity = await db.SupervisorCellulePrimeDrafts.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException();

        if (!string.Equals(entity.SupervisorUserId, supervisorUserId.Trim(), StringComparison.Ordinal) ||
            !await org.SupervisorOwnsCelluleAsync(supervisorUserId, entity.CelluleId, ct))
            throw new UnauthorizedAccessException("Accès refusé pour ce périmètre.");

        db.SupervisorCellulePrimeDrafts.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}
