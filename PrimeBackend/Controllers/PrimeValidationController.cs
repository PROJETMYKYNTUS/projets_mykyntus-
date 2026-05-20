using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;
using PrimeBackend.Services;

namespace PrimeBackend.Controllers;

/// <summary>
/// API de validation des <b>fiches</b> service (Superviseur, Chef de projet).
/// RH / Manager / Comptabilité : <see cref="PrimeGlobalPoolStakeholderController"/>.
/// </summary>
[ApiController]
[Route("api/prime/validation")]
public sealed class PrimeValidationController(
    PrimeDbContext? db,
    IPrimeRequestUserResolver? userResolver,
    PrimeValidationWorkflowRuntime? wfRuntime,
    PrimeRbacReadService? rbac,
    PrimeValidationListService? validationList,
    PrimeFicheValidationSubmissionService? submission,
    AnomalyDetectionService? anomalies,
    GlobalPoolWorkflowService? poolWf) : ControllerBase
{

    private const string GlobalPoolRoleFicheErrorMessage =
        "Ce rôle valide le fichier synthèse globale PRIME (écran « Synthèse globale »), pas les fiches individuelles.";

    private static bool IsBlockedGlobalPoolRoleOnFicheApi(string role) =>
        PrimeFicheValidationRoles.IsGlobalPoolStakeholder(role);

    [HttpGet("workflow-meta")]
    public async Task<ActionResult<WorkflowValidationMetaDto>> WorkflowMeta([FromQuery] string? role, CancellationToken ct)
    {
        if (db == null || wfRuntime == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var steps = await db.WorkflowSteps.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync(ct);
        var dto = steps.Select(s => new WorkflowStepConfigDto
        {
            Id = s.Id,
            SortOrder = s.SortOrder,
            ApproverRole = s.ApproverRole,
            FromStatus = s.FromStatus,
            ToStatus = s.ToStatus,
            IsActive = s.IsActive,
            SlaHours = s.SlaHours,
            CapturesAmountsOnApproval = s.CapturesAmountsOnApproval,
            TerminalApproved = s.TerminalApproved,
            UpdatedAt = s.UpdatedAt,
        }).ToList();
        var terminals = await wfRuntime.GetTerminalStatusesAsync(ct);
        var actionable = string.IsNullOrWhiteSpace(role)
            ? []
            : await wfRuntime.GetActionableFromStatusesForRoleAsync(role.Trim(), ct);
        return Ok(new WorkflowValidationMetaDto
        {
            Steps = dto,
            TerminalStatuses = terminals,
            ActionableFromStatuses = actionable,
        });
    }

    [HttpGet]
    public async Task<ActionResult<List<EmployeePrimeServiceFicheValidationDto>>> List(
        [FromQuery] string? period,
        [FromQuery] string? status,
        [FromQuery] string? serviceId,
        [FromQuery] string? celluleId,
        [FromQuery] string? userId,
        [FromQuery] string? role,
        [FromQuery] bool? readyOnly,
        CancellationToken ct)
    {
        if (db == null || validationList is null)
            return StatusCode(503, new { error = "Base de données non configurée." });

        var ruEarly = userResolver is null ? null : await userResolver.TryResolveAsync(Request, userId, role, ct);
        if (ruEarly is not null && IsBlockedGlobalPoolRoleOnFicheApi(ruEarly.Role))
            return BadRequest(new { error = GlobalPoolRoleFicheErrorMessage });

        var applyReadyOnly = readyOnly ?? PrimeValidationListService.ShouldDefaultReadyOnly(role);

        if (submission is not null)
            await submission.ReconcileReadySubmissionsAsync(ct);

        var query = db.EmployeePrimeServiceFiches.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(period)) query = query.Where(f => f.Period == period.Trim());
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (wfRuntime is null || !await wfRuntime.IsValidFilterStatusAsync(status.Trim(), ct))
                return BadRequest(new { error = "Statut invalide ou inconnu du workflow." });
            query = query.Where(f => f.ValidationStatus == status.Trim());
        }
        else
        {
            query = query.Where(f => f.ValidationStatus != PrimeValidationWorkflowService.AwaitingData);
        }

        if (applyReadyOnly)
            query = validationList.ApplyReadyForValidationFilter(query);

        if (!string.IsNullOrWhiteSpace(serviceId)) query = query.Where(f => f.ServiceId == serviceId.Trim());
        if (!string.IsNullOrWhiteSpace(celluleId)) query = query.Where(f => f.CelluleId == celluleId.Trim());

        var items = await query.OrderByDescending(f => f.UpdatedAt).Take(5000).ToListAsync(ct);
        var ru = userResolver is null ? null : await userResolver.TryResolveAsync(Request, userId, role, ct);
        if (ru is not null && rbac is not null)
        {
            var filtered = new List<EmployeePrimeServiceFicheEntity>();
            foreach (var f in items)
            {
                if (await rbac.CanAccessFicheAsync(ru.Employee, f, "Read", ct) ||
                    await rbac.CanAccessFicheAsync(ru.Employee, f, "Validate", ct))
                    filtered.Add(f);
            }
            items = filtered;
        }

        return Ok(await validationList.MapValidationDtosAsync(items, ct));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<WorkflowValidationSummaryDto>> Summary(
        [FromQuery] string? period,
        [FromQuery] string? serviceId,
        [FromQuery] string? celluleId,
        [FromQuery] string? userId,
        [FromQuery] string? role,
        [FromQuery] bool? readyOnly,
        CancellationToken ct)
    {
        if (db == null || wfRuntime == null || validationList is null)
            return StatusCode(503, new { error = "Base de données non configurée." });

        var applyReadyOnly = readyOnly ?? PrimeValidationListService.ShouldDefaultReadyOnly(role);

        if (submission is not null)
            await submission.ReconcileReadySubmissionsAsync(ct);

        var query = db.EmployeePrimeServiceFiches.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(period)) query = query.Where(f => f.Period == period.Trim());
        if (!string.IsNullOrWhiteSpace(serviceId)) query = query.Where(f => f.ServiceId == serviceId.Trim());
        if (!string.IsNullOrWhiteSpace(celluleId)) query = query.Where(f => f.CelluleId == celluleId.Trim());
        query = query.Where(f => f.ValidationStatus != PrimeValidationWorkflowService.AwaitingData);
        if (applyReadyOnly && validationList is not null)
            query = validationList.ApplyReadyForValidationFilter(query);

        var items = await query.Take(5000).ToListAsync(ct);
        var ru = userResolver is null ? null : await userResolver.TryResolveAsync(Request, userId, role, ct);
        if (ru is not null && rbac is not null)
        {
            var filtered = new List<EmployeePrimeServiceFicheEntity>();
            foreach (var f in items)
            {
                if (await rbac.CanAccessFicheAsync(ru.Employee, f, "Read", ct) ||
                    await rbac.CanAccessFicheAsync(ru.Employee, f, "Validate", ct))
                    filtered.Add(f);
            }
            items = filtered;
        }

        var grouped = items.GroupBy(f => f.ValidationStatus)
            .Select(g => new WorkflowStatusCountDto { Status = g.Key, Count = g.Count() })
            .OrderBy(x => x.Status)
            .ToList();
        var terminals = await wfRuntime.GetTerminalStatusesAsync(ct);
        return Ok(new WorkflowValidationSummaryDto
        {
            StatusCounts = grouped,
            TerminalStatuses = terminals,
            Total = items.Count,
        });
    }

    [HttpGet("periods")]
    public async Task<ActionResult<List<string>>> Periods(CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var list = await db.EmployeePrimeServiceFiches.AsNoTracking()
            .Select(f => f.Period)
            .Distinct()
            .OrderByDescending(p => p)
            .Take(120)
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<EmployeePrimeServiceFicheValidationDto>> Approve(
        Guid id,
        [FromBody] ApproveServiceFicheRequest body,
        CancellationToken ct)
    {
        if (db == null || wfRuntime == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var ru = userResolver is null ? null : await userResolver.TryResolveAsync(Request, body.UserId, body.Role, ct);
        if (ru is null)
            return Unauthorized(new { error = "Utilisateur / rôle invalides ou incohérents avec la base." });

        if (IsBlockedGlobalPoolRoleOnFicheApi(ru.Role))
            return BadRequest(new { error = GlobalPoolRoleFicheErrorMessage });
        if (!PrimeFicheValidationRoles.IsOperationalApprover(ru.Role))
            return StatusCode(403, new { error = "Seuls les rôles opérationnels (Référent technique, Superviseur, Chef de projet) valident les fiches." });

        var fiche = await db.EmployeePrimeServiceFiches.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (fiche == null) return NotFound();

        if (rbac is not null)
        {
            if (!await rbac.RoleHasActionAsync(ru.Role, "Validate", ct))
                return StatusCode(403, new { error = "Action « Validate » non autorisée pour ce rôle (RBAC)." });
            if (!await rbac.CanAccessFicheAsync(ru.Employee, fiche, "Validate", ct))
                return StatusCode(403, new { error = "Périmètre RBAC insuffisant pour cette fiche." });
        }

        var (ok, err, next, step) = await wfRuntime.TryResolveApprovalAsync(fiche, ru.Role, ct);
        if (!ok || next is null) return BadRequest(new { error = err ?? "Transition impossible." });

        PrimeValidationWorkflowService.ApplyApproval(fiche, next, ru.UserId, DateTimeOffset.UtcNow);

        if (step?.CapturesAmountsOnApproval == true)
        {
            if (fiche.PrimeAmount is null && body.PrimeAmount is not null) fiche.PrimeAmount = body.PrimeAmount;
            if (fiche.ChallengeAmount is null && body.ChallengeAmount is not null) fiche.ChallengeAmount = body.ChallengeAmount;
            if (fiche.TotalAmount is null && body.TotalAmount is not null) fiche.TotalAmount = body.TotalAmount;
        }

        await db.SaveChangesAsync(ct);
        if (anomalies is not null)
            await anomalies.RecomputeForFicheAsync(fiche.Id, ct);
        if (validationList is null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await validationList.MapValidationDtoAsync(fiche, ct));
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<EmployeePrimeServiceFicheValidationDto>> Reject(
        Guid id,
        [FromBody] RejectServiceFicheRequest body,
        CancellationToken ct)
    {
        if (db == null || wfRuntime == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var global = await db.WorkflowGlobalConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        if (global?.RequireRejectReason == true && string.IsNullOrWhiteSpace(body.Reason))
            return BadRequest(new { error = "Un motif de rejet est obligatoire." });

        var ru = userResolver is null ? null : await userResolver.TryResolveAsync(Request, body.UserId, body.Role, ct);
        if (ru is null)
            return Unauthorized(new { error = "Utilisateur / rôle invalides ou incohérents avec la base." });

        if (IsBlockedGlobalPoolRoleOnFicheApi(ru.Role))
            return BadRequest(new { error = GlobalPoolRoleFicheErrorMessage });
        if (!PrimeFicheValidationRoles.IsOperationalApprover(ru.Role))
            return StatusCode(403, new { error = "Seuls les rôles opérationnels (Référent technique, Superviseur, Chef de projet) valident les fiches." });

        var fiche = await db.EmployeePrimeServiceFiches.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (fiche == null) return NotFound();

        if (rbac is not null)
        {
            if (!await rbac.RoleHasActionAsync(ru.Role, "Validate", ct))
                return StatusCode(403, new { error = "Action « Validate » non autorisée pour ce rôle (RBAC)." });
            if (!await rbac.CanAccessFicheAsync(ru.Employee, fiche, "Validate", ct))
                return StatusCode(403, new { error = "Périmètre RBAC insuffisant pour cette fiche." });
        }

        if (!await wfRuntime.CanRejectAsync(fiche.ValidationStatus, ru.Role, ct))
            return BadRequest(new { error = $"Le rôle « {ru.Role} » ne peut pas rejeter depuis l'état « {fiche.ValidationStatus} »." });
        if (string.IsNullOrWhiteSpace(body.Reason))
            return BadRequest(new { error = "Un motif de rejet est obligatoire." });
        PrimeValidationWorkflowService.ApplyReject(fiche, ru.UserId, body.Reason.Trim(), DateTimeOffset.UtcNow);

        await db.SaveChangesAsync(ct);
        if (anomalies is not null)
            await anomalies.RecomputeForFicheAsync(fiche.Id, ct);
        if (validationList is null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await validationList.MapValidationDtoAsync(fiche, ct));
    }

    [HttpPost("bulk-approve")]
    public async Task<ActionResult<object>> BulkApprove(
        [FromBody] BulkApproveServiceFicheRequest body,
        CancellationToken ct)
    {
        if (db == null || wfRuntime == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var global = await db.WorkflowGlobalConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        if (global?.AllowBulkApprove == false)
            return BadRequest(new { error = "L'approbation groupée est désactivée dans la configuration workflow." });

        var ru = userResolver is null ? null : await userResolver.TryResolveAsync(Request, body.UserId, body.Role, ct);
        if (ru is null)
            return Unauthorized(new { error = "Utilisateur / rôle invalides ou incohérents avec la base." });

        if (IsBlockedGlobalPoolRoleOnFicheApi(ru.Role))
            return BadRequest(new { error = GlobalPoolRoleFicheErrorMessage });
        if (!PrimeFicheValidationRoles.IsOperationalApprover(ru.Role))
            return StatusCode(403, new { error = "Seuls les rôles opérationnels (Référent technique, Superviseur, Chef de projet) valident les fiches." });

        if (rbac is not null)
        {
            if (!await rbac.RoleHasActionAsync(ru.Role, "Validate", ct))
                return StatusCode(403, new { error = "Action « Validate » non autorisée pour ce rôle (RBAC)." });
        }

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
            if (rbac is not null && !await rbac.CanAccessFicheAsync(ru.Employee, f, "Validate", ct))
            {
                ignored.Add(f.Id);
                continue;
            }
            var (ok, _, next, _) = await wfRuntime.TryResolveApprovalAsync(f, ru.Role, ct);
            if (!ok || next is null)
            {
                ignored.Add(f.Id);
                continue;
            }
            PrimeValidationWorkflowService.ApplyApproval(f, next, ru.UserId, now);
            approved.Add(f.Id);
        }
        await db.SaveChangesAsync(ct);
        if (anomalies is not null)
        {
            foreach (var id in approved)
                await anomalies.RecomputeForFicheAsync(id, ct);
        }
        return Ok(new { approvedIds = approved, ignoredIds = ignored });
    }

    private async Task<bool> PoolDistributionUnlockedForFicheAsync(EmployeePrimeServiceFicheEntity fiche, CancellationToken ct)
    {
        if (db == null || poolWf == null) return false;
        var draft = await db.SupervisorCellulePrimeDrafts.AsNoTracking()
            .Where(d => d.Period == fiche.Period && d.CelluleId == fiche.CelluleId)
            .OrderByDescending(d => d.UpdatedAt)
            .FirstOrDefaultAsync(ct);
        if (draft is null) return false;
        return await poolWf.PoolDistributionUnlockedAsync(draft, ct);
    }

    private async Task<IActionResult?> GuardPiloteExportAsync(
        EmployeePrimeServiceFicheEntity fiche,
        string? userId,
        string? role,
        CancellationToken ct)
    {
        var ru = userResolver is null ? null : await userResolver.TryResolveAsync(Request, userId, role, ct);
        var actorRole = ru?.Role ?? role;
        if (!PrimeFicheDistributionAccess.RoleMustWaitForPrimeDistribution(actorRole))
            return null;
        var unlocked = await PoolDistributionUnlockedForFicheAsync(fiche, ct);
        if (!PrimeFicheDistributionAccess.CanAccessMergedFicheLivrable(actorRole, unlocked))
            return StatusCode(403, new { error = "Export indisponible : validations PRIME en cours." });
        return null;
    }

    [HttpGet("{id:guid}/export-csv")]
    public async Task<IActionResult> ExportCsv(
        Guid id,
        [FromQuery] string? userId,
        [FromQuery] string? role,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var fiche = await db.EmployeePrimeServiceFiches.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);
        if (fiche == null) return NotFound();
        var guard = await GuardPiloteExportAsync(fiche, userId, role, ct);
        if (guard is not null) return guard;
        var sb = new StringBuilder();
        sb.AppendLine("Period,EmployeeId,ServiceId,CelluleId,ValidationStatus,FillingStatus,PrimeAmount,ChallengeAmount,TotalAmount");
        sb.Append(CsvEscape(fiche.Period)).Append(',');
        sb.Append(CsvEscape(fiche.EmployeeId)).Append(',');
        sb.Append(CsvEscape(fiche.ServiceId)).Append(',');
        sb.Append(CsvEscape(fiche.CelluleId)).Append(',');
        sb.Append(CsvEscape(fiche.ValidationStatus)).Append(',');
        sb.Append(CsvEscape(fiche.FillingStatus)).Append(',');
        sb.Append(fiche.PrimeAmount?.ToString("F2") ?? "").Append(',');
        sb.Append(fiche.ChallengeAmount?.ToString("F2") ?? "").Append(',');
        sb.Append(fiche.TotalAmount?.ToString("F2") ?? "");
        sb.AppendLine();
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv; charset=utf-8", $"fiche-prime-{fiche.Id}.csv");
    }

    [HttpGet("{id:guid}/export-xlsx")]
    public async Task<IActionResult> ExportXlsx(
        Guid id,
        [FromQuery] string? userId,
        [FromQuery] string? role,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var fiche = await db.EmployeePrimeServiceFiches.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);
        if (fiche == null) return NotFound();
        var guard = await GuardPiloteExportAsync(fiche, userId, role, ct);
        if (guard is not null) return guard;
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Fiche");
        ws.Cell(1, 1).Value = "Période";
        ws.Cell(1, 2).Value = fiche.Period;
        ws.Cell(2, 1).Value = "Employé";
        ws.Cell(2, 2).Value = fiche.EmployeeId;
        ws.Cell(3, 1).Value = "Statut validation";
        ws.Cell(3, 2).Value = fiche.ValidationStatus;
        ws.Cell(4, 1).Value = "Prime";
        ws.Cell(4, 2).Value = fiche.PrimeAmount;
        ws.Cell(5, 1).Value = "Challenge";
        ws.Cell(5, 2).Value = fiche.ChallengeAmount;
        ws.Cell(6, 1).Value = "Total";
        ws.Cell(6, 2).Value = fiche.TotalAmount;
        await using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"fiche-prime-{fiche.Id}.xlsx");
    }

    private static string CsvEscape(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return $"\"{s.Replace("\"", "\"\"")}\"";
        return s;
    }
}
