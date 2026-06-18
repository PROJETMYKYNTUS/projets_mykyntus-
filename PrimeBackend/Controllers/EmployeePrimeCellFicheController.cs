using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;
using PrimeBackend.Infrastructure;
using PrimeBackend.Services;

namespace PrimeBackend.Controllers;

[ApiController]
[Route("api/prime/employee-prime-service-fiches")]
[Route("api/prime/employee-prime-cell-fiches")] // alias : clients encore sur l’ancien chemin (ex. Angular 4202)
public sealed class EmployeePrimeServiceFicheController(
    PrimeDbContext? db,
    PrimeOrgScopeService org,
    PrimeFicheValidationSubmissionService? submission,
    AnomalyDetectionService? anomalies) : ControllerBase
{
    private static EmployeePrimeServiceFicheResponseDto Map(
        EmployeePrimeServiceFicheEntity e,
        SupervisorCellulePrimeDraftEntity draft) =>
        new()
        {
            Id = e.Id,
            CellulePrimeDraftId = e.CellulePrimeDraftId,
            SupervisorUserId = e.SupervisorUserId,
            EmployeeId = e.EmployeeId,
            ServiceId = e.ServiceId,
            CelluleId = e.CelluleId,
            Period = e.Period,
            ServiceSaisieJson = e.ServiceSaisieJson,
            FillingStatus = e.FillingStatus,
            ValidationStatus = e.ValidationStatus,
            IsReadyForValidation = PrimeFicheValidationSubmissionService.ComputeIsReadyForValidation(draft, e),
            UpdatedAt = e.UpdatedAt,
        };

    [HttpGet("list")]
    public async Task<ActionResult<List<EmployeePrimeServiceFicheListItemDto>>> List(
        [FromQuery] string? serviceId,
        [FromQuery] string? celluleId,
        [FromQuery] string period,
        [FromQuery] string supervisorUserId,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(period) || string.IsNullOrWhiteSpace(supervisorUserId))
            return BadRequest(new { error = "period et supervisorUserId sont requis." });

        var hasService = !string.IsNullOrWhiteSpace(serviceId);
        var hasCellule = !string.IsNullOrWhiteSpace(celluleId);
        if (!hasService && !hasCellule)
            return BadRequest(new { error = "Indiquez serviceId (équipe) ou celluleId (cellule entière)." });
        // Si les deux sont fournis, on privilégie serviceId (plus précis).
        if (hasService && hasCellule) hasCellule = false;

        var per = period.Trim();
        var sup = supervisorUserId.Trim();

        List<EmployeeEntity> emps;
        List<EmployeePrimeServiceFicheEntity> fiches;

        if (hasService)
        {
            var cid = serviceId!.Trim();
            var resolvedCellule = await org.GetCelluleIdForServiceAsync(cid, ct);
            if (resolvedCellule is null) return NotFound(new { error = "Service introuvable." });
            if (!await org.SupervisorOwnsCelluleAsync(sup, resolvedCellule, ct))
                return StatusCode(403, new { error = "Accès refusé pour ce périmètre." });

            emps = await org.GetPilotsInServiceAsync(cid, ct);
            fiches = await db.EmployeePrimeServiceFiches.AsNoTracking()
                .Where(f => f.ServiceId == cid && f.Period == per)
                .ToListAsync(ct);
        }
        else
        {
            var cTrim = celluleId!.Trim();
            if (!await org.SupervisorOwnsCelluleAsync(sup, cTrim, ct))
                return StatusCode(403, new { error = "Accès refusé pour ce périmètre." });

            emps = await org.GetPilotsInCelluleAsync(cTrim, ct);
            if (emps.Count == 0) return Ok(new List<EmployeePrimeServiceFicheListItemDto>());

            var serviceIds = emps.Select(e => e.ServiceId).Distinct(StringComparer.Ordinal).ToList();
            fiches = await db.EmployeePrimeServiceFiches.AsNoTracking()
                .Where(f => f.Period == per && serviceIds.Contains(f.ServiceId))
                .ToListAsync(ct);
        }

        var byEmp = fiches
            .GroupBy(f => f.EmployeeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First(), StringComparer.Ordinal);

        if (submission is not null && fiches.Count > 0)
        {
            await submission.ReconcileReadySubmissionsForSupervisorPeriodAsync(sup, per, ct);
            if (hasService)
            {
                var sid = serviceId!.Trim();
                fiches = await db.EmployeePrimeServiceFiches.AsNoTracking()
                    .Where(f => f.ServiceId == sid && f.Period == per)
                    .ToListAsync(ct);
            }
            else
            {
                var serviceIdsReload = emps.Select(e => e.ServiceId).Distinct(StringComparer.Ordinal).ToList();
                fiches = await db.EmployeePrimeServiceFiches.AsNoTracking()
                    .Where(f => f.Period == per && serviceIdsReload.Contains(f.ServiceId))
                    .ToListAsync(ct);
            }

            byEmp = fiches
                .GroupBy(f => f.EmployeeId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First(), StringComparer.Ordinal);
        }

        var result = new List<EmployeePrimeServiceFicheListItemDto>();
        foreach (var e in emps)
        {
            if (byEmp.TryGetValue(e.Id, out var f))
            {
                var ready = submission is not null &&
                            await submission.ComputeIsReadyForValidationAsync(f, ct);
                result.Add(new EmployeePrimeServiceFicheListItemDto
                {
                    EmployeeId = e.Id,
                    FirstName = e.FirstName,
                    LastName = e.LastName,
                    Email = e.Email,
                    ServiceId = e.ServiceId,
                    FicheId = f.Id,
                    CellulePrimeDraftId = f.CellulePrimeDraftId,
                    FillingStatus = f.FillingStatus,
                    ValidationStatus = f.ValidationStatus,
                    IsReadyForValidation = ready,
                    ServiceSaisieJson = f.ServiceSaisieJson,
                    UpdatedAt = f.UpdatedAt,
                });
            }
            else
            {
                result.Add(new EmployeePrimeServiceFicheListItemDto
                {
                    EmployeeId = e.Id,
                    FirstName = e.FirstName,
                    LastName = e.LastName,
                    Email = e.Email,
                    ServiceId = e.ServiceId,
                    FicheId = null,
                    CellulePrimeDraftId = null,
                    FillingStatus = "NotStarted",
                    ServiceSaisieJson = "{}",
                    UpdatedAt = null,
                });
            }
        }

        return Ok(result);
    }

    [HttpGet("for-employee")]
    public async Task<ActionResult<EmployeePrimeServiceFicheResponseDto>> GetForEmployee(
        [FromQuery] string supervisorUserId,
        [FromQuery] string employeeId,
        [FromQuery] string period,
        [FromQuery] string? templateId,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(supervisorUserId) || string.IsNullOrWhiteSpace(employeeId) ||
            string.IsNullOrWhiteSpace(period))
            return BadRequest(new { error = "supervisorUserId, employeeId et period sont requis." });

        var emp = await org.GetEmployeeAsync(employeeId, ct);
        if (emp is null) return NotFound(new { error = "Employé introuvable." });
        if (!await org.SupervisorOwnsCelluleAsync(supervisorUserId, emp.CelluleId, ct))
            return StatusCode(403, new { error = "Accès refusé pour ce périmètre." });

        var sup = supervisorUserId.Trim();
        var per = period.Trim();
        SupervisorCellulePrimeDraftEntity? draft;
        if (!string.IsNullOrWhiteSpace(templateId))
        {
            draft = await db.SupervisorCellulePrimeDrafts.AsNoTracking().FirstOrDefaultAsync(
                x => x.SupervisorUserId == sup && x.CelluleId == emp.CelluleId && x.Period == per &&
                     x.TemplateId == templateId.Trim(), ct);
        }
        else
        {
            draft = await db.SupervisorCellulePrimeDrafts.AsNoTracking()
                .Where(x => x.SupervisorUserId == sup && x.CelluleId == emp.CelluleId && x.Period == per)
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefaultAsync(ct);
        }

        if (draft is null)
            return NotFound(new
            {
                error = string.IsNullOrWhiteSpace(templateId)
                    ? "Aucun brouillon pôle pour ce pôle et cette période. Enregistrez d’abord la partie commune (RACC/SAV) dans « Fiche PRIME — saisie »."
                    : "Brouillon pôle introuvable pour cette période et ce template.",
            });

        var fiche = await db.EmployeePrimeServiceFiches.AsNoTracking().FirstOrDefaultAsync(
            x => x.EmployeeId == emp.Id && x.Period == period.Trim(), ct);
        if (fiche is null)
        {
            return Ok(new EmployeePrimeServiceFicheResponseDto
            {
                Id = Guid.Empty,
                CellulePrimeDraftId = draft.Id,
                SupervisorUserId = supervisorUserId.Trim(),
                EmployeeId = emp.Id,
                ServiceId = emp.ServiceId,
                CelluleId = emp.CelluleId,
                Period = period.Trim(),
                ServiceSaisieJson = "{}",
                FillingStatus = "NotStarted",
                ValidationStatus = PrimeValidationWorkflowService.AwaitingData,
                IsReadyForValidation = false,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }

        return Ok(Map(fiche, draft));
    }

    [HttpPut]
    public async Task<ActionResult<EmployeePrimeServiceFicheResponseDto>> Upsert(
        [FromBody] UpsertEmployeePrimeServiceFicheRequest body,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });

        if (body.CellulePrimeDraftId == Guid.Empty && body.PolePrimeDraftId is Guid pp && pp != Guid.Empty)
            body.CellulePrimeDraftId = pp;
        if (!string.IsNullOrWhiteSpace(body.CellSaisieJson) &&
            (string.IsNullOrWhiteSpace(body.ServiceSaisieJson) ||
             string.Equals(body.ServiceSaisieJson.Trim(), "{}", StringComparison.Ordinal)))
            body.ServiceSaisieJson = body.CellSaisieJson.Trim();

        if (string.IsNullOrWhiteSpace(body.SupervisorUserId) || string.IsNullOrWhiteSpace(body.EmployeeId) ||
            string.IsNullOrWhiteSpace(body.Period) || body.CellulePrimeDraftId == Guid.Empty)
            return BadRequest(new { error = "Champs obligatoires manquants." });

        var emp = await org.GetEmployeeAsync(body.EmployeeId, ct);
        if (emp is null) return NotFound(new { error = "Employé introuvable." });
        if (!await org.SupervisorOwnsCelluleAsync(body.SupervisorUserId, emp.CelluleId, ct))
            return StatusCode(403, new { error = "Accès refusé pour ce périmètre." });

        var draft = await db.SupervisorCellulePrimeDrafts.FirstOrDefaultAsync(x => x.Id == body.CellulePrimeDraftId, ct);
        if (draft is null) return NotFound(new { error = "Brouillon pôle introuvable." });
        if (!string.Equals(draft.SupervisorUserId, body.SupervisorUserId.Trim(), StringComparison.Ordinal) ||
            !string.Equals(draft.CelluleId, emp.CelluleId, StringComparison.Ordinal) ||
            !string.Equals(draft.Period, body.Period.Trim(), StringComparison.Ordinal))
            return BadRequest(new { error = "Le brouillon pôle ne correspond pas à l’employé ou à la période." });

        var indicators = await db.ServicePrimeIndicators.AsNoTracking()
            .Where(i => i.ServiceId == emp.ServiceId)
            .OrderBy(i => i.SortOrder)
            .ToListAsync(ct);
        if (PrimeEmployeeFicheAmountService.HasNegativeFinancialValuesInServiceSaisieJson(body.ServiceSaisieJson))
            return BadRequest(new { error = DbExceptionMessages.NonNegativePrimeAmountsRequired });
        var status = PrimeServiceFicheStatusHelper.ComputeFillingStatus(body.ServiceSaisieJson, indicators);
        var now = DateTimeOffset.UtcNow;

        var entity = await db.EmployeePrimeServiceFiches.FirstOrDefaultAsync(
            x => x.EmployeeId == emp.Id && x.Period == body.Period.Trim(), ct);

        if (entity == null)
        {
            entity = new EmployeePrimeServiceFicheEntity
            {
                Id = Guid.NewGuid(),
                CellulePrimeDraftId = draft.Id,
                SupervisorUserId = body.SupervisorUserId.Trim(),
                EmployeeId = emp.Id,
                ServiceId = emp.ServiceId,
                CelluleId = emp.CelluleId,
                Period = body.Period.Trim(),
                ServiceSaisieJson = body.ServiceSaisieJson,
                FillingStatus = status,
                ValidationStatus = PrimeValidationWorkflowService.AwaitingData,
                UpdatedAt = now,
            };
            db.EmployeePrimeServiceFiches.Add(entity);
        }
        else
        {
            entity.CellulePrimeDraftId = draft.Id;
            entity.SupervisorUserId = body.SupervisorUserId.Trim();
            entity.ServiceId = emp.ServiceId;
            entity.CelluleId = emp.CelluleId;
            entity.ServiceSaisieJson = body.ServiceSaisieJson;
            entity.FillingStatus = status;
            entity.UpdatedAt = now;
        }

        if (submission is not null)
            await submission.SyncValidationSubmissionStatusAsync(entity, draft, now, ct);

        // Ne pas ecraser un snapshot deja en place (notamment les montants calcules par le
        // frontend via le moteur de formules) avec une extraction brute qui ne sait pas evaluer
        // les formules (renverrait 0/valeur parasite). On ne calcule l'extraction que lorsqu'aucun
        // montant n'a encore ete enregistre.
        var hasExistingSnapshot =
            entity.PrimeAmount.HasValue || entity.ChallengeAmount.HasValue || entity.TotalAmount.HasValue;
        if (!hasExistingSnapshot)
        {
            var amounts = PrimeEmployeeFicheAmountService.ExtractFromFiche(entity);
            PrimeEmployeeFicheAmountService.ApplySnapshotToEntity(entity, amounts);
        }

        await db.SaveChangesAsync(ct);
        return Ok(Map(entity, draft));
    }

    /// <summary>
    /// Persiste les montants finaux (Prime / Challenge / Total) calcules par le frontend
    /// (ligne « TOTAL General » de la fiche fusionnee) sur la fiche identifiee.
    /// </summary>
    [HttpPost("{ficheId:guid}/amounts")]
    public async Task<ActionResult<EmployeePrimeServiceFicheResponseDto>> PersistAmounts(
        Guid ficheId,
        [FromBody] PersistFicheAmountsRequest body,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(body.SupervisorUserId))
            return BadRequest(new { error = "supervisorUserId est requis." });

        var entity = await db.EmployeePrimeServiceFiches.FirstOrDefaultAsync(x => x.Id == ficheId, ct);
        if (entity is null) return NotFound(new { error = "Fiche introuvable." });

        if (!await org.SupervisorOwnsCelluleAsync(body.SupervisorUserId, entity.CelluleId, ct))
            return StatusCode(403, new { error = "Accès refusé pour ce périmètre." });

        if (!PrimeEmployeeFicheAmountService.IsNonNegative(body.PrimeAmount) ||
            !PrimeEmployeeFicheAmountService.IsNonNegative(body.ChallengeAmount) ||
            !PrimeEmployeeFicheAmountService.IsNonNegative(body.TotalAmount))
            return BadRequest(new { error = DbExceptionMessages.NonNegativePrimeAmountsRequired });

        entity.PrimeAmount = body.PrimeAmount;
        entity.ChallengeAmount = body.ChallengeAmount;
        entity.TotalAmount = body.TotalAmount;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        if (anomalies is not null)
            await anomalies.RecomputeForFicheAsync(entity.Id, ct);

        var draft = await db.SupervisorCellulePrimeDrafts.FirstOrDefaultAsync(x => x.Id == entity.CellulePrimeDraftId, ct);
        return draft is null ? Ok(new { ok = true }) : Ok(Map(entity, draft));
    }
}
