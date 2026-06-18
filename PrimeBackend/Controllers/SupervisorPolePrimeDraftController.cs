using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;
using PrimeBackend.Infrastructure;
using PrimeBackend.Services;

namespace PrimeBackend.Controllers;

[ApiController]
[Route("api/prime/supervisor-cellule-prime-drafts")]
[Route("api/prime/supervisor-pole-prime-drafts")] // alias : clients encore sur l’ancien chemin (ex. module Angular 4202)
public sealed class SupervisorCellulePrimeDraftController(
    PrimeDbContext? db,
    PrimeOrgScopeService org,
    PrimeFicheValidationSubmissionService? submission) : ControllerBase
{
    private static SupervisorCellulePrimeDraftResponseDto Map(SupervisorCellulePrimeDraftEntity e) =>
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

    [HttpGet]
    public async Task<ActionResult<SupervisorCellulePrimeDraftResponseDto>> Get(
        [FromQuery] string supervisorUserId,
        [FromQuery] string? celluleId,
        [FromQuery] string? poleId,
        [FromQuery] string period,
        [FromQuery] string templateId,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var rawKey = !string.IsNullOrWhiteSpace(celluleId) ? celluleId.Trim() : (poleId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(supervisorUserId) || string.IsNullOrWhiteSpace(rawKey) ||
            string.IsNullOrWhiteSpace(period) || string.IsNullOrWhiteSpace(templateId))
            return BadRequest(new { error = "supervisorUserId, celluleId (ou poleId), period et templateId sont requis." });

        var celluleCanon = await org.NormalizeSupervisorDraftCelluleKeyAsync(supervisorUserId, rawKey, ct);
        if (celluleCanon is null)
            return StatusCode(403, new { error = "Accès refusé pour ce périmètre." });

        var entity = await db.SupervisorCellulePrimeDrafts.AsNoTracking().FirstOrDefaultAsync(
            x => x.SupervisorUserId == supervisorUserId.Trim() && x.CelluleId == celluleCanon &&
                 x.Period == period.Trim() && x.TemplateId == templateId.Trim(), ct);
        if (entity == null) return NotFound();
        return Ok(Map(entity));
    }

    /// <summary>
    /// Liste les fiches communes « en cours » du superviseur — tous pôles supervisés confondus.
    /// Filtre les fiches totalement terminées (Status=Validated ET tous les employés des cellules
    /// du pôle ont FillingStatus=Complete). Les fiches filtrées partent en historiques.
    /// </summary>
    [HttpGet("list-active")]
    public async Task<ActionResult<List<SupervisorCellulePrimeDraftListItemDto>>> ListActive(
        [FromQuery] string supervisorUserId,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(supervisorUserId))
            return BadRequest(new { error = "supervisorUserId est requis." });

        var supTrim = supervisorUserId.Trim();
        var celluleIds = await org.GetSupervisedCelluleIdsAsync(supTrim, ct);
        if (celluleIds.Count == 0) return Ok(new List<SupervisorCellulePrimeDraftListItemDto>());

        var drafts = await db.SupervisorCellulePrimeDrafts.AsNoTracking()
            .Where(d => d.SupervisorUserId == supTrim && celluleIds.Contains(d.CelluleId))
            .ToListAsync(ct);
        if (drafts.Count == 0) return Ok(new List<SupervisorCellulePrimeDraftListItemDto>());

        // Pré-charge les fiches employé pour tous les drafts (1 seule requête).
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
            if (isFullyComplete) continue; // filtré — irait dans les historiques

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

        return Ok(result
            .OrderByDescending(r => r.Period)
            .ThenByDescending(r => r.UpdatedAt)
            .ToList());
    }

    [HttpPut]
    public async Task<ActionResult<SupervisorCellulePrimeDraftResponseDto>> Upsert(
        [FromBody] UpsertSupervisorCellulePrimeDraftRequest body,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });

        // Compat clients encore sur l’ancien contrat JSON (poleId / poleSaisieJson).
        if (string.IsNullOrWhiteSpace(body.CelluleId) && !string.IsNullOrWhiteSpace(body.PoleId))
            body.CelluleId = body.PoleId.Trim();
        if (!string.IsNullOrWhiteSpace(body.PoleSaisieJson) &&
            (string.IsNullOrWhiteSpace(body.CelluleSaisieJson) || string.Equals(body.CelluleSaisieJson.Trim(), "{}", StringComparison.Ordinal)))
            body.CelluleSaisieJson = body.PoleSaisieJson.Trim();

        if (string.IsNullOrWhiteSpace(body.SupervisorUserId) || string.IsNullOrWhiteSpace(body.CelluleId) ||
            string.IsNullOrWhiteSpace(body.Period) || string.IsNullOrWhiteSpace(body.TemplateId))
            return BadRequest(new { error = "Champs obligatoires manquants." });

        var celluleCanon = await org.NormalizeSupervisorDraftCelluleKeyAsync(body.SupervisorUserId, body.CelluleId, ct);
        if (celluleCanon is null)
            return StatusCode(403, new { error = "Accès refusé pour ce périmètre (identifiant cellule / pôle non reconnu pour ce superviseur)." });
        body.CelluleId = celluleCanon;

        var poleSaisieToStore = CelluleDraftPayloadNormalizer.NormalizeCelluleSaisieJson(body.SchemaJson, body.CelluleSaisieJson);

        var supTrim = body.SupervisorUserId.Trim();
        var poleTrim = body.CelluleId.Trim();
        var periodTrim = body.Period.Trim();
        var templateTrim = body.TemplateId.Trim();

        var rootPoleId = await org.ResolveRootPoleIdForCelluleAsync(poleTrim, ct);
        if (string.IsNullOrWhiteSpace(rootPoleId))
            return BadRequest(new { error = "Impossible de résoudre le pôle racine pour cette cellule. Vérifiez la structure RH (prime_pole / prime_cellule)." });
        var now = DateTimeOffset.UtcNow;
        var entity = await db.SupervisorCellulePrimeDrafts.FirstOrDefaultAsync(
            x => x.SupervisorUserId == supTrim && x.RootPoleId == rootPoleId && x.Period == periodTrim, ct);

        if (entity == null)
        {
            // Unicité : une fiche commune par (superviseur, pôle racine, période).
            var stale = await db.SupervisorCellulePrimeDrafts
                .Where(x => x.SupervisorUserId == supTrim && x.RootPoleId == rootPoleId && x.Period == periodTrim)
                .ToListAsync(ct);
            if (stale.Count > 0) db.SupervisorCellulePrimeDrafts.RemoveRange(stale);

            entity = new SupervisorCellulePrimeDraftEntity
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
            return Conflict(new { error = DbExceptionMessages.FromSaveChanges(ex) });
        }

        if (submission is not null)
        {
            await submission.SyncForDraftAsync(entity.Id, ct);
            await db.SaveChangesAsync(ct);
        }

        return Ok(Map(entity));
    }

    /// <summary>Suppression d'une fiche commune en cours (cascade les EmployeeFiches enfants).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromQuery] string supervisorUserId,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(supervisorUserId))
            return BadRequest(new { error = "supervisorUserId est requis." });

        var entity = await db.SupervisorCellulePrimeDrafts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null) return NotFound();

        if (!string.Equals(entity.SupervisorUserId, supervisorUserId.Trim(), StringComparison.Ordinal) ||
            !await org.SupervisorOwnsCelluleAsync(supervisorUserId, entity.CelluleId, ct))
            return StatusCode(403, new { error = "Accès refusé pour ce périmètre." });

        db.SupervisorCellulePrimeDrafts.Remove(entity);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
