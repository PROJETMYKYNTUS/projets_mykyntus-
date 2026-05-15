using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;
using PrimeBackend.Services;

namespace PrimeBackend.Controllers;

/// <summary>
/// API de validation des fiches service (Phase 1.1+1.2).
/// Expose les transitions du workflow (approve/reject/bulk + summary) sur
/// <see cref="EmployeePrimeServiceFicheEntity"/>.
/// </summary>
[ApiController]
[Route("api/prime/validation")]
public sealed class PrimeValidationController(PrimeDbContext? db) : ControllerBase
{
    private static EmployeePrimeServiceFicheValidationDto Map(EmployeePrimeServiceFicheEntity e) => new()
    {
        Id = e.Id,
        EmployeeId = e.EmployeeId,
        SupervisorUserId = e.SupervisorUserId,
        ServiceId = e.ServiceId,
        CelluleId = e.CelluleId,
        Period = e.Period,
        FillingStatus = e.FillingStatus,
        ValidationStatus = e.ValidationStatus,
        LastApproverUserId = e.LastApproverUserId,
        LastApprovedAt = e.LastApprovedAt,
        RejectedByUserId = e.RejectedByUserId,
        RejectedAt = e.RejectedAt,
        RejectionReason = e.RejectionReason,
        PrimeAmount = e.PrimeAmount,
        ChallengeAmount = e.ChallengeAmount,
        TotalAmount = e.TotalAmount,
        UpdatedAt = e.UpdatedAt,
    };

    /// <summary>Liste filtrée par période et statut (optionnel).</summary>
    [HttpGet]
    public async Task<ActionResult<List<EmployeePrimeServiceFicheValidationDto>>> List(
        [FromQuery] string? period,
        [FromQuery] string? status,
        [FromQuery] string? serviceId,
        [FromQuery] string? celluleId,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });

        var query = db.EmployeePrimeServiceFiches.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(period)) query = query.Where(f => f.Period == period.Trim());
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!PrimeValidationWorkflowService.IsValidStatus(status.Trim()))
                return BadRequest(new { error = "Statut invalide." });
            query = query.Where(f => f.ValidationStatus == status.Trim());
        }
        if (!string.IsNullOrWhiteSpace(serviceId)) query = query.Where(f => f.ServiceId == serviceId.Trim());
        if (!string.IsNullOrWhiteSpace(celluleId)) query = query.Where(f => f.CelluleId == celluleId.Trim());

        var items = await query.OrderByDescending(f => f.UpdatedAt).ToListAsync(ct);
        return Ok(items.Select(Map).ToList());
    }

    /// <summary>Récapitulatif compteurs par statut (filtrable par période / périmètre).</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<WorkflowStatusSummaryDto>> Summary(
        [FromQuery] string? period,
        [FromQuery] string? serviceId,
        [FromQuery] string? celluleId,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });

        var query = db.EmployeePrimeServiceFiches.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(period)) query = query.Where(f => f.Period == period.Trim());
        if (!string.IsNullOrWhiteSpace(serviceId)) query = query.Where(f => f.ServiceId == serviceId.Trim());
        if (!string.IsNullOrWhiteSpace(celluleId)) query = query.Where(f => f.CelluleId == celluleId.Trim());

        var grouped = await query
            .GroupBy(f => f.ValidationStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        int Find(string s) => grouped.FirstOrDefault(x => x.Status == s)?.Count ?? 0;
        return Ok(new WorkflowStatusSummaryDto
        {
            Pending = Find(PrimeValidationWorkflowService.Pending),
            ReferentTechniqueApproved = Find(PrimeValidationWorkflowService.ReferentTechniqueApproved),
            SuperviseurApproved = Find(PrimeValidationWorkflowService.SuperviseurApproved),
            ChefDeProjetApproved = Find(PrimeValidationWorkflowService.ChefDeProjetApproved),
            RhApproved = Find(PrimeValidationWorkflowService.RhApproved),
            Rejected = Find(PrimeValidationWorkflowService.Rejected),
            Total = grouped.Sum(g => g.Count),
        });
    }

    /// <summary>Approuve une fiche en faisant avancer son statut d'un cran.</summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<EmployeePrimeServiceFicheValidationDto>> Approve(
        Guid id,
        [FromBody] ApproveServiceFicheRequest body,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(body.UserId) || string.IsNullOrWhiteSpace(body.Role))
            return BadRequest(new { error = "UserId et Role sont obligatoires." });

        var fiche = await db.EmployeePrimeServiceFiches.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (fiche == null) return NotFound();

        try
        {
            PrimeValidationWorkflowService.ApproveOrThrow(fiche, body.UserId.Trim(), body.Role.Trim(), DateTimeOffset.UtcNow);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }

        // Snapshot des montants après validation Superviseur (1er cran du flux) si fournis et pas déjà fixés.
        if (fiche.ValidationStatus == PrimeValidationWorkflowService.SuperviseurApproved)
        {
            if (fiche.PrimeAmount is null && body.PrimeAmount is not null) fiche.PrimeAmount = body.PrimeAmount;
            if (fiche.ChallengeAmount is null && body.ChallengeAmount is not null) fiche.ChallengeAmount = body.ChallengeAmount;
            if (fiche.TotalAmount is null && body.TotalAmount is not null) fiche.TotalAmount = body.TotalAmount;
        }

        await db.SaveChangesAsync(ct);
        return Ok(Map(fiche));
    }

    /// <summary>Rejette une fiche avec motif obligatoire.</summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<EmployeePrimeServiceFicheValidationDto>> Reject(
        Guid id,
        [FromBody] RejectServiceFicheRequest body,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(body.UserId) || string.IsNullOrWhiteSpace(body.Role))
            return BadRequest(new { error = "UserId et Role sont obligatoires." });
        if (string.IsNullOrWhiteSpace(body.Reason))
            return BadRequest(new { error = "Un motif de rejet est obligatoire." });

        var fiche = await db.EmployeePrimeServiceFiches.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (fiche == null) return NotFound();

        try
        {
            PrimeValidationWorkflowService.RejectOrThrow(fiche, body.UserId.Trim(), body.Role.Trim(), body.Reason, DateTimeOffset.UtcNow);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }

        await db.SaveChangesAsync(ct);
        return Ok(Map(fiche));
    }

    /// <summary>Approbation groupée — ignore silencieusement les fiches non éligibles, retourne les ids effectivement approuvés.</summary>
    [HttpPost("bulk-approve")]
    public async Task<ActionResult<object>> BulkApprove(
        [FromBody] BulkApproveServiceFicheRequest body,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(body.UserId) || string.IsNullOrWhiteSpace(body.Role))
            return BadRequest(new { error = "UserId et Role sont obligatoires." });
        if (body.FicheIds is null || body.FicheIds.Count == 0)
            return BadRequest(new { error = "Aucune fiche fournie." });

        var fiches = await db.EmployeePrimeServiceFiches
            .Where(f => body.FicheIds.Contains(f.Id))
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var approved = new List<Guid>();
        var ignored = new List<Guid>();
        foreach (var f in fiches)
        {
            try
            {
                PrimeValidationWorkflowService.ApproveOrThrow(f, body.UserId.Trim(), body.Role.Trim(), now);
                approved.Add(f.Id);
            }
            catch
            {
                ignored.Add(f.Id);
            }
        }
        await db.SaveChangesAsync(ct);
        return Ok(new { approvedIds = approved, ignoredIds = ignored });
    }
}
