using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;
using PrimeBackend.Services;

namespace PrimeBackend.Controllers;

[ApiController]
[Route("api/prime/employee-prime-cell-fiches")]
public sealed class EmployeePrimeCellFicheController(PrimeDbContext? db, PrimeInMemoryStore store) : ControllerBase
{
    private static EmployeePrimeCellFicheResponseDto Map(EmployeePrimeCellFicheEntity e) =>
        new()
        {
            Id = e.Id,
            PolePrimeDraftId = e.PolePrimeDraftId,
            SupervisorUserId = e.SupervisorUserId,
            EmployeeId = e.EmployeeId,
            CelluleId = e.CelluleId,
            PoleId = e.PoleId,
            Period = e.Period,
            CellSaisieJson = e.CellSaisieJson,
            FillingStatus = e.FillingStatus,
            UpdatedAt = e.UpdatedAt,
        };

    [HttpGet("list")]
    public async Task<ActionResult<List<EmployeePrimeCellFicheListItemDto>>> List(
        [FromQuery] string celluleId,
        [FromQuery] string period,
        [FromQuery] string supervisorUserId,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(celluleId) || string.IsNullOrWhiteSpace(period) ||
            string.IsNullOrWhiteSpace(supervisorUserId))
            return BadRequest(new { error = "celluleId, period et supervisorUserId sont requis." });

        var poleId = store.GetPoleIdForCellule(celluleId);
        if (poleId is null) return NotFound(new { error = "Cellule introuvable." });
        if (!store.SupervisorOwnsPole(supervisorUserId, poleId))
            return StatusCode(403, new { error = "Accès refusé pour ce périmètre." });

        var cid = celluleId.Trim();
        var per = period.Trim();
        var emps = store.GetEmployees()
            .Where(e => string.Equals(e.CelluleId, cid, StringComparison.Ordinal))
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .ToList();

        var fiches = await db.EmployeePrimeCellFiches.AsNoTracking()
            .Where(f => f.CelluleId == cid && f.Period == per)
            .ToListAsync(ct);
        var byEmp = fiches.ToDictionary(f => f.EmployeeId, StringComparer.Ordinal);

        var result = new List<EmployeePrimeCellFicheListItemDto>();
        foreach (var e in emps)
        {
            if (byEmp.TryGetValue(e.Id, out var f))
            {
                result.Add(new EmployeePrimeCellFicheListItemDto
                {
                    EmployeeId = e.Id,
                    FirstName = e.FirstName,
                    LastName = e.LastName,
                    Email = e.Email,
                    CelluleId = e.CelluleId,
                    FicheId = f.Id,
                    PolePrimeDraftId = f.PolePrimeDraftId,
                    FillingStatus = f.FillingStatus,
                    CellSaisieJson = f.CellSaisieJson,
                    UpdatedAt = f.UpdatedAt,
                });
            }
            else
            {
                result.Add(new EmployeePrimeCellFicheListItemDto
                {
                    EmployeeId = e.Id,
                    FirstName = e.FirstName,
                    LastName = e.LastName,
                    Email = e.Email,
                    CelluleId = e.CelluleId,
                    FicheId = null,
                    PolePrimeDraftId = null,
                    FillingStatus = "NotStarted",
                    CellSaisieJson = "{}",
                    UpdatedAt = null,
                });
            }
        }

        return Ok(result);
    }

    [HttpGet("for-employee")]
    public async Task<ActionResult<EmployeePrimeCellFicheResponseDto>> GetForEmployee(
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

        var emp = store.GetEmployees().FirstOrDefault(e => e.Id == employeeId.Trim());
        if (emp is null) return NotFound(new { error = "Employé introuvable." });
        if (!store.SupervisorOwnsPole(supervisorUserId, emp.PoleId))
            return StatusCode(403, new { error = "Accès refusé pour ce périmètre." });

        var sup = supervisorUserId.Trim();
        var per = period.Trim();
        SupervisorPolePrimeDraftEntity? draft;
        if (!string.IsNullOrWhiteSpace(templateId))
        {
            draft = await db.SupervisorPolePrimeDrafts.AsNoTracking().FirstOrDefaultAsync(
                x => x.SupervisorUserId == sup && x.PoleId == emp.PoleId && x.Period == per &&
                     x.TemplateId == templateId.Trim(), ct);
        }
        else
        {
            draft = await db.SupervisorPolePrimeDrafts.AsNoTracking()
                .Where(x => x.SupervisorUserId == sup && x.PoleId == emp.PoleId && x.Period == per)
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

        var fiche = await db.EmployeePrimeCellFiches.AsNoTracking().FirstOrDefaultAsync(
            x => x.EmployeeId == emp.Id && x.Period == period.Trim(), ct);
        if (fiche is null)
        {
            return Ok(new EmployeePrimeCellFicheResponseDto
            {
                Id = Guid.Empty,
                PolePrimeDraftId = draft.Id,
                SupervisorUserId = supervisorUserId.Trim(),
                EmployeeId = emp.Id,
                CelluleId = emp.CelluleId,
                PoleId = emp.PoleId,
                Period = period.Trim(),
                CellSaisieJson = "{}",
                FillingStatus = "NotStarted",
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }

        return Ok(Map(fiche));
    }

    [HttpPut]
    public async Task<ActionResult<EmployeePrimeCellFicheResponseDto>> Upsert(
        [FromBody] UpsertEmployeePrimeCellFicheRequest body,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(body.SupervisorUserId) || string.IsNullOrWhiteSpace(body.EmployeeId) ||
            string.IsNullOrWhiteSpace(body.Period) || body.PolePrimeDraftId == Guid.Empty)
            return BadRequest(new { error = "Champs obligatoires manquants." });

        var emp = store.GetEmployees().FirstOrDefault(e => e.Id == body.EmployeeId.Trim());
        if (emp is null) return NotFound(new { error = "Employé introuvable." });
        if (!store.SupervisorOwnsPole(body.SupervisorUserId, emp.PoleId))
            return StatusCode(403, new { error = "Accès refusé pour ce périmètre." });

        var draft = await db.SupervisorPolePrimeDrafts.FirstOrDefaultAsync(x => x.Id == body.PolePrimeDraftId, ct);
        if (draft is null) return NotFound(new { error = "Brouillon pôle introuvable." });
        if (!string.Equals(draft.SupervisorUserId, body.SupervisorUserId.Trim(), StringComparison.Ordinal) ||
            !string.Equals(draft.PoleId, emp.PoleId, StringComparison.Ordinal) ||
            !string.Equals(draft.Period, body.Period.Trim(), StringComparison.Ordinal))
            return BadRequest(new { error = "Le brouillon pôle ne correspond pas à l’employé ou à la période." });

        var indicators = await db.CellulePrimeIndicators.AsNoTracking()
            .Where(i => i.CelluleId == emp.CelluleId)
            .OrderBy(i => i.SortOrder)
            .ToListAsync(ct);
        var status = PrimeCellFicheStatusHelper.ComputeFillingStatus(body.CellSaisieJson, indicators);
        var now = DateTimeOffset.UtcNow;

        var entity = await db.EmployeePrimeCellFiches.FirstOrDefaultAsync(
            x => x.EmployeeId == emp.Id && x.Period == body.Period.Trim(), ct);

        if (entity == null)
        {
            entity = new EmployeePrimeCellFicheEntity
            {
                Id = Guid.NewGuid(),
                PolePrimeDraftId = draft.Id,
                SupervisorUserId = body.SupervisorUserId.Trim(),
                EmployeeId = emp.Id,
                CelluleId = emp.CelluleId,
                PoleId = emp.PoleId,
                Period = body.Period.Trim(),
                CellSaisieJson = body.CellSaisieJson,
                FillingStatus = status,
                UpdatedAt = now,
            };
            db.EmployeePrimeCellFiches.Add(entity);
        }
        else
        {
            entity.PolePrimeDraftId = draft.Id;
            entity.SupervisorUserId = body.SupervisorUserId.Trim();
            entity.CelluleId = emp.CelluleId;
            entity.PoleId = emp.PoleId;
            entity.CellSaisieJson = body.CellSaisieJson;
            entity.FillingStatus = status;
            entity.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return Ok(Map(entity));
    }
}
