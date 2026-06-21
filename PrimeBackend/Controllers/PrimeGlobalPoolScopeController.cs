using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;
using PrimeBackend.Infrastructure;
using PrimeBackend.Services;

namespace PrimeBackend.Controllers;

/// <summary>Synthèse globale PRIME par périmètre (service / cellule / pôle).</summary>
[ApiController]
[Route("api/prime/global-pool")]
public sealed class PrimeGlobalPoolScopeController(
    PrimeDbContext? db,
    IPrimeRequestUserResolver? userResolver,
    GlobalPoolWorkflowService? poolWf,
    PrimeGlobalSynthesisReadinessService? readiness,
    PrimeGlobalSynthesisService? synthesis,
    PrimeGlobalSynthesisLineService? lineService,
    PrimeGlobalSynthesisPaymentService? paymentService,
    PrimeFicheValidationHistoryService? validationHistory,
    PrimeRbacReadService? rbac) : ControllerBase
{
    private static bool LegacyScopeUnlocked(GlobalPoolScopeSynthesisEntity s) =>
        s.ManagerApprovedAt.HasValue && s.RhApprovedAt.HasValue;

    private async Task<string?> RoleOfUserAsync(string userId, CancellationToken ct)
    {
        if (db is null) return null;
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId.Trim(), ct);
        return e?.Role;
    }

    private async Task<bool> ManagesOperationalDepartmentAsync(string userId, CancellationToken ct)
    {
        if (db is null) return false;
        return await db.BusinessDepartments.AsNoTracking()
            .AnyAsync(d => d.ManagerEmployeeId == userId && d.IsActive && d.Kind == "Operational", ct);
    }

    /// <summary>
    /// Résout l'acteur d'une action de synthèse (valider / rejeter / payer).
    /// L'utilisateur doit exister et son rôle réel doit autoriser la synthèse.
    /// Un Admin peut endosser le rôle sélectionné (Manager / RH / Comptabilité) ;
    /// les managers opérationnels (BusinessDepartmentKind) agissent en Manager malgré le rôle stocké Superviseur.
    /// </summary>
    private async Task<(PrimeResolvedUser? user, ActionResult? error)> ResolvePoolActorAsync(
        string? userId, string? declaredRole, CancellationToken ct)
    {
        if (userResolver is null)
            return (null, StatusCode(503, new { error = "Base de données non configurée." }));

        var resolved = await userResolver.TryResolveForValidationAsync(Request, userId, declaredRole, ct);
        if (resolved is null)
            return (null, Unauthorized(new { error = "Utilisateur invalide." }));

        var realRole = resolved.Employee.Role?.Trim() ?? "";
        var declared = resolved.Role?.Trim() ?? "";
        var managesOperational = await ManagesOperationalDepartmentAsync(resolved.UserId, ct);
        var actingRole = PrimeGlobalPoolActorResolver.ResolveActingRole(
            resolved.Employee, realRole, declared, managesOperational);
        if (actingRole is null)
            return (null, StatusCode(403, new { error = "Rôle non autorisé pour la validation de synthèse." }));

        return (new PrimeResolvedUser(resolved.UserId, actingRole, resolved.Employee), null);
    }

    [HttpGet("readiness")]
    public async Task<ActionResult<GlobalPoolReadinessDto>> Readiness([FromQuery] string period, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(period)) return BadRequest(new { error = "period est requis." });
        if (readiness is null) return StatusCode(503, new { error = "Base de données non configurée." });
        return Ok(await readiness.GetReadinessAsync(period, ct));
    }

    [HttpGet("synthesis/lines")]
    public async Task<ActionResult<GlobalSynthesisLinesResponseDto>> SynthesisLines(
        [FromQuery] string period,
        [FromQuery] string scopeType,
        [FromQuery] string scopeId,
        [FromQuery] Guid? scopeSynthesisId,
        [FromQuery] string? userId,
        CancellationToken ct)
    {
        if (synthesis is null || readiness is null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (!GlobalPoolScopeTypes.IsValid(scopeType)) return BadRequest(new { error = "scopeType invalide." });

        Guid? sid = scopeSynthesisId;
        if (!sid.HasValue && !string.IsNullOrWhiteSpace(userId))
        {
            var role = await RoleOfUserAsync(userId, ct);
            if (PrimeGlobalPoolActorResolver.IsPoolStakeholderRole(role) && await readiness.IsScopeReadyAsync(period, scopeType, scopeId, ct))
            {
                var entity = await synthesis.EnsureAsync(period, scopeType, scopeId, userId.Trim(), ct);
                sid = entity?.Id;
            }
        }

        var lines = await synthesis.ListLinesAsync(period, scopeType, scopeId, sid, ct);
        return Ok(new GlobalSynthesisLinesResponseDto
        {
            ScopeSynthesisId = sid,
            ValidationReady = sid.HasValue && lines.Any(l => l.LineId.HasValue),
            Lines = lines,
        });
    }

    [HttpGet("synthesis/summary")]
    public async Task<ActionResult<GlobalSynthesisSummaryDto>> SynthesisSummary(
        [FromQuery] string period,
        [FromQuery] string scopeType,
        [FromQuery] string scopeId,
        [FromQuery] Guid? scopeSynthesisId,
        CancellationToken ct)
    {
        if (synthesis is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var lines = await synthesis.ListLinesAsync(period, scopeType, scopeId, scopeSynthesisId, ct);
        return Ok(PrimeGlobalSynthesisService.Summarize(lines));
    }

    [HttpPost("synthesis/generate")]
    public async Task<IActionResult> GenerateSynthesis([FromBody] GenerateScopeSynthesisRequest body, CancellationToken ct)
    {
        if (db is null || synthesis is null || readiness is null)
            return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(body.UserId)) return BadRequest(new { error = "userId est requis." });
        var role = await RoleOfUserAsync(body.UserId, ct);
        if (role is not ("RH" or "Admin" or "Manager"))
            return StatusCode(403, new { error = "Génération réservée à RH, Manager ou Admin." });

        try
        {
            var (entity, _) = await synthesis.GenerateAsync(body.Period, body.ScopeType, body.ScopeId, body.UserId, ct);
            return Ok(new { scopeSynthesisId = entity.Id, fileName = entity.FileName, generatedAt = entity.GeneratedAt });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("synthesis/ensure")]
    public async Task<IActionResult> EnsureSynthesis([FromBody] GenerateScopeSynthesisRequest body, CancellationToken ct)
    {
        if (db is null || synthesis is null || readiness is null)
            return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(body.UserId)) return BadRequest(new { error = "userId est requis." });
        if (!GlobalPoolScopeTypes.IsValid(body.ScopeType)) return BadRequest(new { error = "scopeType invalide." });
        var role = await RoleOfUserAsync(body.UserId, ct);
        if (!PrimeGlobalPoolActorResolver.IsPoolStakeholderRole(role))
            return StatusCode(403, new { error = "Rôle non autorisé." });

        try
        {
            var entity = await synthesis.EnsureAsync(body.Period, body.ScopeType, body.ScopeId, body.UserId, ct);
            if (entity is null)
                return Ok(new { scopeSynthesisId = (Guid?)null, ready = false, error = "Périmètre non prêt pour la synthèse." });
            return Ok(new { scopeSynthesisId = (Guid?)entity.Id, ready = true, fileName = entity.FileName, generatedAt = entity.GeneratedAt });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("scope-inbox")]
    public async Task<ActionResult<List<GlobalPoolScopeSynthesisInboxItemDto>>> ScopeInbox(
        [FromQuery] string userId,
        [FromQuery] string? role,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId)) return BadRequest(new { error = "userId est requis." });
        if (db is null || poolWf is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (actor, authErr) = await ResolvePoolActorAsync(userId, role, ct);
        if (authErr is not null) return authErr;
        var poolRole = actor!.Role;

        var list = await db.GlobalPoolScopeSyntheses.AsNoTracking()
            .Where(s => s.ExcelContent != null && s.ExcelContent.Length > 0)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync(ct);

        var result = new List<GlobalPoolScopeSynthesisInboxItemDto>();
        foreach (var s in list)
            result.Add(await MapScopeInboxAsync(s, poolRole, ct));
        return Ok(result);
    }

    [HttpGet("scope-synthesis/{scopeSynthesisId:guid}/excel")]
    public async Task<IActionResult> DownloadScopeExcel(Guid scopeSynthesisId, [FromQuery] string userId, CancellationToken ct)
    {
        if (db is null || synthesis is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var s = await db.GlobalPoolScopeSyntheses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == scopeSynthesisId, ct);
        if (s?.ExcelContent is not { Length: > 0 }) return NotFound();
        var role = await RoleOfUserAsync(userId, ct);
        if (string.IsNullOrWhiteSpace(role)) return BadRequest(new { error = "Utilisateur inconnu." });
        var legacyOk = LegacyScopeUnlocked(s);
        var fullyUnlocked = poolWf is not null && await poolWf.PoolDistributionUnlockedAsync(s, ct);
        var hasApprovedLines = await db.GlobalPoolSynthesisLines.AsNoTracking()
            .AnyAsync(l => l.ScopeSynthesisId == s.Id && l.LineStatus == GlobalPoolSynthesisLineStatuses.Approved, ct);
        if (!PrimeFicheDistributionAccess.CanDownloadGlobalPoolSynthesis(role, legacyOk, fullyUnlocked, hasApprovedLines))
            return StatusCode(403, new { error = "Fichier non diffusé : validations en attente." });

        var approvedExcel = await synthesis.BuildApprovedExportExcelAsync(scopeSynthesisId, ct);
        var content = approvedExcel is { Length: > 0 } ? approvedExcel : s.ExcelContent;
        var name = string.IsNullOrWhiteSpace(s.FileName) ? "prime-synthese.xlsx" : s.FileName.Trim();
        return File(content!, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    [HttpPost("scope-synthesis/{scopeSynthesisId:guid}/approve-step")]
    public async Task<IActionResult> ApproveScopeStep(
        Guid scopeSynthesisId,
        [FromBody] GlobalPoolApproveStepRequest body,
        CancellationToken ct)
    {
        if (db is null || poolWf is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var ru = userResolver is null ? null : await userResolver.TryResolveAsync(Request, body.UserId, body.Role, ct);
        if (ru is null) return Unauthorized(new { error = "Utilisateur invalide." });
        var role = await RoleOfUserAsync(ru.UserId, ct);
        if (string.IsNullOrWhiteSpace(role)) return BadRequest(new { error = "Utilisateur inconnu." });
        var s = await db.GlobalPoolScopeSyntheses.FirstOrDefaultAsync(x => x.Id == scopeSynthesisId, ct);
        if (s is null) return NotFound();
        if (!await poolWf.UsesConfigurableWorkflowAsync(ct))
            return BadRequest(new { error = "Workflow configurable requis ou routes legacy scope." });
        var (ok, msg) = await poolWf.TryApproveScopeStepAsync(s, body.StepId, ru.UserId, role, ct);
        if (!ok) return BadRequest(new { error = msg });
        return Ok(await MapScopeInboxAsync(s, role, ct));
    }

    [HttpPost("scope-synthesis/{scopeSynthesisId:guid}/approve-manager")]
    public async Task<IActionResult> ApproveScopeManager(Guid scopeSynthesisId, [FromBody] GlobalPoolActingUserRequest body, CancellationToken ct)
    {
        if (db is null || poolWf is null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (await poolWf.UsesConfigurableWorkflowAsync(ct))
            return BadRequest(new { error = "Utilisez approve-step." });
        var role = await RoleOfUserAsync(body.UserId, ct);
        if (role is not ("Manager" or "Admin")) return StatusCode(403, new { error = "Manager uniquement." });
        var s = await db.GlobalPoolScopeSyntheses.FirstOrDefaultAsync(x => x.Id == scopeSynthesisId, ct);
        if (s is null) return NotFound();
        var now = DateTimeOffset.UtcNow;
        s.ManagerApprovedAt = now;
        s.ManagerApprovedByUserId = body.UserId.Trim();
        s.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return Ok(await MapScopeInboxAsync(s, role!, ct));
    }

    [HttpPost("scope-synthesis/{scopeSynthesisId:guid}/approve-rh")]
    public async Task<IActionResult> ApproveScopeRh(Guid scopeSynthesisId, [FromBody] GlobalPoolActingUserRequest body, CancellationToken ct)
    {
        if (db is null || poolWf is null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (await poolWf.UsesConfigurableWorkflowAsync(ct))
            return BadRequest(new { error = "Utilisez approve-step." });
        var role = await RoleOfUserAsync(body.UserId, ct);
        if (role is not ("RH" or "Admin")) return StatusCode(403, new { error = "RH uniquement." });
        var s = await db.GlobalPoolScopeSyntheses.FirstOrDefaultAsync(x => x.Id == scopeSynthesisId, ct);
        if (s is null) return NotFound();
        var now = DateTimeOffset.UtcNow;
        s.RhApprovedAt = now;
        s.RhApprovedByUserId = body.UserId.Trim();
        s.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return Ok(await MapScopeInboxAsync(s, role!, ct));
    }

    [HttpPost("scope-synthesis/{scopeSynthesisId:guid}/ack-compta")]
    public async Task<IActionResult> AckScopeCompta(Guid scopeSynthesisId, [FromBody] GlobalPoolActingUserRequest body, CancellationToken ct)
    {
        if (db is null || poolWf is null || synthesis is null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (await poolWf.UsesConfigurableWorkflowAsync(ct))
            return BadRequest(new { error = "Utilisez approve-step." });
        var role = await RoleOfUserAsync(body.UserId, ct);
        if (role is not ("Comptable" or "Comptabilité" or "Admin")) return StatusCode(403, new { error = "Compta uniquement." });
        var s = await db.GlobalPoolScopeSyntheses.FirstOrDefaultAsync(x => x.Id == scopeSynthesisId, ct);
        if (s is null) return NotFound();
        if (!LegacyScopeUnlocked(s))
            return BadRequest(new { error = "Validations Manager et RH requises." });
        var now = DateTimeOffset.UtcNow;
        var approvedExcel = await synthesis.BuildApprovedExportExcelAsync(scopeSynthesisId, ct);
        if (approvedExcel is { Length: > 0 })
            s.ExcelContent = approvedExcel;
        s.ComptaAckAt = now;
        s.ComptaAckByUserId = body.UserId.Trim();
        s.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return Ok(await MapScopeInboxAsync(s, role!, ct));
    }

    [HttpPost("synthesis/lines/{lineId:guid}/reject")]
    public async Task<IActionResult> RejectLine(Guid lineId, [FromBody] RejectSynthesisLineRequest body, CancellationToken ct)
    {
        if (lineService is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (ru, authErr) = await ResolvePoolActorAsync(body.UserId, body.Role, ct);
        if (authErr is not null) return authErr;
        try
        {
            var (ok, msg) = await lineService.RejectLineAsync(lineId, ru!.UserId, ru.Role, body.Reason, ct);
            if (!ok) return BadRequest(new { error = msg });
            return Ok(new { ok = true });
        }
        catch (DbUpdateException ex)
        {
            return Conflict(new { error = DbExceptionMessages.FromSaveChanges(ex) });
        }
    }

    [HttpPost("synthesis/lines/{lineId:guid}/approve")]
    public async Task<IActionResult> ApproveLine(Guid lineId, [FromBody] GlobalPoolActingUserRequest body, CancellationToken ct)
    {
        if (lineService is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (ru, authErr) = await ResolvePoolActorAsync(body.UserId, body.Role, ct);
        if (authErr is not null) return authErr;
        try
        {
            var (ok, msg) = await lineService.ApproveLineAsync(lineId, ru!.UserId, ru.Role, ct);
            if (!ok) return BadRequest(new { error = msg });
            return Ok(new { ok = true });
        }
        catch (DbUpdateException ex)
        {
            return Conflict(new { error = DbExceptionMessages.FromSaveChanges(ex) });
        }
    }

    [HttpGet("supervisor-synthesis-tracking")]
    public async Task<ActionResult<List<SupervisorSynthesisTrackingItemDto>>> SupervisorSynthesisTracking(
        [FromQuery] string supervisorUserId,
        [FromQuery] string period,
        CancellationToken ct)
    {
        if (db is null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (string.IsNullOrWhiteSpace(supervisorUserId)) return BadRequest(new { error = "supervisorUserId est requis." });
        var sup = supervisorUserId.Trim();
        var per = period?.Trim();

        var rows = await (
            from f in db.EmployeePrimeServiceFiches.AsNoTracking()
            where f.SupervisorUserId == sup && (per == null || per == "" || f.Period == per)
            join emp in db.Employees.AsNoTracking() on f.EmployeeId equals emp.Id
            join srv in db.Services.AsNoTracking() on f.ServiceId equals srv.Id
            join cel in db.Cellules.AsNoTracking() on srv.CelluleId equals cel.Id
            select new { f, emp, srv, cel }
        ).ToListAsync(ct);
        if (rows.Count == 0) return Ok(new List<SupervisorSynthesisTrackingItemDto>());

        var ficheIds = rows.Select(r => r.f.Id).ToList();
        var lines = await (
            from l in db.GlobalPoolSynthesisLines.AsNoTracking()
            where ficheIds.Contains(l.FicheId)
            join s in db.GlobalPoolScopeSyntheses.AsNoTracking() on l.ScopeSynthesisId equals s.Id
            select new { l, s }
        ).ToListAsync(ct);

        // Une fiche peut figurer dans plusieurs synthèses (service/cellule/pôle) : prendre la plus récente.
        var lineByFiche = lines
            .GroupBy(x => x.l.FicheId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.s.UpdatedAt).First());

        var result = rows.Select(r =>
        {
            lineByFiche.TryGetValue(r.f.Id, out var match);
            var syn = match?.s;
            var unlocked = syn is not null && syn.ManagerApprovedAt.HasValue && syn.RhApprovedAt.HasValue;
            return new SupervisorSynthesisTrackingItemDto
            {
                FicheId = r.f.Id,
                EmployeeId = r.emp.Id,
                EmployeeDisplayName = $"{r.emp.FirstName} {r.emp.LastName}".Trim(),
                CelluleName = r.cel.Name,
                ServiceName = r.srv.Name,
                ValidationStatus = r.f.ValidationStatus,
                LineStatus = match?.l.LineStatus,
                RhDecision = match?.l.RhDecision ?? GlobalPoolLineDecisions.Pending,
                ManagerDecision = match?.l.ManagerDecision ?? GlobalPoolLineDecisions.Pending,
                RhRejectionReason = match?.l.RhRejectionReason,
                ManagerRejectionReason = match?.l.ManagerRejectionReason,
                RejectedByRole = match?.l.RejectedByRole,
                PaymentStatus = match?.l.PaymentStatus ?? GlobalPoolPaymentStatuses.Unpaid,
                PaidAt = match?.l.PaidAt,
                ManagerApproved = syn?.ManagerApprovedAt.HasValue ?? false,
                RhApproved = syn?.RhApprovedAt.HasValue ?? false,
                PoolDistributionUnlocked = unlocked,
                ScopeSynthesisId = syn?.Id,
                ScopeLabel = syn is null ? null : $"{syn.ScopeType} {syn.ScopeDisplayName} ({syn.Period})",
            };
        })
        .OrderBy(x => x.CelluleName).ThenBy(x => x.ServiceName).ThenBy(x => x.EmployeeDisplayName)
        .ToList();

        return Ok(result);
    }

    [HttpGet("my-synthesis-tracking")]
    public async Task<ActionResult<List<EmployeePrimePaymentTrackingDto>>> MySynthesisTracking(
        [FromQuery] string? userId,
        [FromQuery] string? role,
        CancellationToken ct)
    {
        if (db is null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (userResolver is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var ru = await userResolver.TryResolveAsync(Request, userId, role, ct);
        if (ru is null) return Unauthorized(new { error = "Utilisateur invalide." });
        var realRole = ru.Employee.Role?.Trim() ?? "";
        if (realRole is not ("Pilote" or "Admin"))
            return StatusCode(403, new { error = "Réservé au pilote." });
        var employeeId = ru.UserId;

        var rows = await (
            from f in db.EmployeePrimeServiceFiches.AsNoTracking()
            where f.EmployeeId == employeeId
            join srv in db.Services.AsNoTracking() on f.ServiceId equals srv.Id
            join cel in db.Cellules.AsNoTracking() on srv.CelluleId equals cel.Id
            select new { f, srv, cel }
        ).ToListAsync(ct);
        if (rows.Count == 0) return Ok(new List<EmployeePrimePaymentTrackingDto>());

        var ficheIds = rows.Select(r => r.f.Id).ToList();
        var lines = await (
            from l in db.GlobalPoolSynthesisLines.AsNoTracking()
            where ficheIds.Contains(l.FicheId)
            join s in db.GlobalPoolScopeSyntheses.AsNoTracking() on l.ScopeSynthesisId equals s.Id
            select new { l, s }
        ).ToListAsync(ct);
        // Une fiche peut figurer dans plusieurs synthèses : prendre la plus récente.
        var lineByFiche = lines
            .GroupBy(x => x.l.FicheId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.s.UpdatedAt).First().l);

        var result = rows.Select(r =>
        {
            lineByFiche.TryGetValue(r.f.Id, out var line);
            var approved = line is not null &&
                string.Equals(line.LineStatus, GlobalPoolSynthesisLineStatuses.Approved, StringComparison.Ordinal);
            return new EmployeePrimePaymentTrackingDto
            {
                FicheId = r.f.Id,
                Period = r.f.Period,
                CelluleName = r.cel.Name,
                ServiceName = r.srv.Name,
                PrimeAmount = line?.PrimeAmount,
                ChallengeAmount = line?.ChallengeAmount,
                TotalAmount = line?.TotalAmount,
                LineStatus = line?.LineStatus,
                PaymentStatus = line?.PaymentStatus ?? GlobalPoolPaymentStatuses.Unpaid,
                PaidAt = line?.PaidAt,
                PaymentReference = line?.PaymentReference,
                CanViewFiche = approved,
            };
        })
        .OrderByDescending(x => x.Period).ThenBy(x => x.ServiceName)
        .ToList();
        return Ok(result);
    }

    [HttpGet("synthesis-tracking-feed")]
    public async Task<ActionResult<List<PrimeFicheValidationHistoryFeedItemDto>>> SynthesisTrackingFeed(
        [FromQuery] string? userId,
        [FromQuery] string? role,
        [FromQuery] string? period,
        [FromQuery] bool? mineOnly,
        [FromQuery] string? action,
        CancellationToken ct)
    {
        if (db is null || validationHistory is null || rbac is null)
            return StatusCode(503, new { error = "Base de données non configurée." });
        PrimeResolvedUser? ru = null;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var (actor, authErr) = await ResolvePoolActorAsync(userId, role, ct);
            if (authErr is not null) return authErr;
            ru = actor;
        }
        if (!string.IsNullOrWhiteSpace(action))
        {
            var a = action.Trim();
            if (!string.Equals(a, PrimeFicheValidationHistoryActions.Approved, StringComparison.Ordinal) &&
                !string.Equals(a, PrimeFicheValidationHistoryActions.Rejected, StringComparison.Ordinal) &&
                !string.Equals(a, "LineRejected", StringComparison.Ordinal) &&
                !string.Equals(a, GlobalPoolSynthesisLineHistoryActions.Paid, StringComparison.Ordinal) &&
                !string.Equals(a, GlobalPoolSynthesisLineHistoryActions.Unpaid, StringComparison.Ordinal))
                return BadRequest(new { error = "Filtre action invalide." });
        }
        var items = await validationHistory.ListSynthesisTrackingFeedAsync(
            ru, rbac, period, mineOnly ?? true, action, 500, ct);
        return Ok(items);
    }

    [HttpGet("synthesis/lines/{lineId:guid}/history")]
    public async Task<ActionResult<List<GlobalPoolSynthesisLineHistoryDto>>> SynthesisLineHistory(
        Guid lineId,
        [FromQuery] string? userId,
        [FromQuery] string? role,
        CancellationToken ct)
    {
        if (db is null || validationHistory is null || rbac is null)
            return StatusCode(503, new { error = "Base de données non configurée." });
        PrimeResolvedUser? ru = null;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var (actor, authErr) = await ResolvePoolActorAsync(userId, role, ct);
            if (authErr is not null) return authErr;
            ru = actor;
        }
        var rows = await validationHistory.ListSynthesisLineHistoryAsync(lineId, ru, rbac, ct);
        if (rows.Count == 0)
        {
            var exists = await db.GlobalPoolSynthesisLines.AsNoTracking()
                .AnyAsync(l => l.Id == lineId, ct);
            if (!exists) return NotFound();
        }
        return Ok(rows);
    }

    [HttpPost("synthesis/lines/{lineId:guid}/payment")]
    public async Task<IActionResult> SetLinePayment(Guid lineId, [FromBody] SetSynthesisLinePaymentRequest body, CancellationToken ct)
    {
        if (paymentService is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (ru, authErr) = await ResolvePoolActorAsync(body.UserId, body.Role, ct);
        if (authErr is not null) return authErr;
        var (ok, msg) = await paymentService.SetLinePaymentAsync(
            lineId, ru!.UserId, ru.Role, body.Paid, body.PaidAt, body.Reference, ct);
        if (!ok) return BadRequest(new { error = msg });
        return Ok(new { ok = true });
    }

    [HttpPost("scope-synthesis/{scopeSynthesisId:guid}/pay-all")]
    public async Task<IActionResult> PayAll(Guid scopeSynthesisId, [FromBody] PaySynthesisAllRequest body, CancellationToken ct)
    {
        if (paymentService is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (ru, authErr) = await ResolvePoolActorAsync(body.UserId, body.Role, ct);
        if (authErr is not null) return authErr;
        var (ok, msg) = await paymentService.PayAllAsync(
            scopeSynthesisId, ru!.UserId, ru.Role, body.PaidAt, body.Reference, ct);
        if (!ok) return BadRequest(new { error = msg });
        return Ok(new { ok = true });
    }

    [HttpGet("periods")]
    public async Task<ActionResult<List<string>>> ListPeriods(CancellationToken ct)
    {
        if (db is null) return StatusCode(503, new { error = "Base de données non configurée." });
        var periods = await db.EmployeePrimeServiceFiches.AsNoTracking()
            .Select(f => f.Period)
            .Distinct()
            .OrderByDescending(p => p)
            .ToListAsync(ct);
        return Ok(periods);
    }

    private async Task<GlobalPoolScopeSynthesisInboxItemDto> MapScopeInboxAsync(
        GlobalPoolScopeSynthesisEntity s,
        string employeeRole,
        CancellationToken ct)
    {
        var pending = poolWf is not null && await poolWf.UsesConfigurableWorkflowAsync(ct)
            ? await poolWf.PendingActionForScopeAsync(s, employeeRole, ct)
            : employeeRole switch
            {
                "Manager" => !s.ManagerApprovedAt.HasValue,
                "RH" => !s.RhApprovedAt.HasValue,
                "Comptable" or "Comptabilité" => LegacyScopeUnlocked(s) && !s.ComptaAckAt.HasValue,
                "Admin" => !s.ManagerApprovedAt.HasValue || !s.RhApprovedAt.HasValue ||
                           (LegacyScopeUnlocked(s) && !s.ComptaAckAt.HasValue),
                _ => false,
            };
        var unlocked = poolWf is not null && await poolWf.PoolDistributionUnlockedAsync(s, ct);
        var totalLines = 0;
        var paidLines = 0;
        var rhDecidedLines = 0;
        var managerDecidedLines = 0;
        var approvedLines = 0;
        var rejectedLines = 0;
        if (db is not null)
        {
            var lines = await db.GlobalPoolSynthesisLines.AsNoTracking()
                .Where(l => l.ScopeSynthesisId == s.Id)
                .Select(l => new { l.PaymentStatus, l.RhDecision, l.ManagerDecision, l.LineStatus })
                .ToListAsync(ct);
            totalLines = lines.Count;
            paidLines = lines.Count(l => l.PaymentStatus == GlobalPoolPaymentStatuses.Paid);
            rhDecidedLines = lines.Count(l => l.RhDecision != GlobalPoolLineDecisions.Pending);
            managerDecidedLines = lines.Count(l => l.ManagerDecision != GlobalPoolLineDecisions.Pending);
            approvedLines = lines.Count(l => l.LineStatus == GlobalPoolSynthesisLineStatuses.Approved);
            rejectedLines = lines.Count(l => l.LineStatus == GlobalPoolSynthesisLineStatuses.LineRejected);
        }
        // Le paiement ne concerne que les lignes validées par les deux workflows.
        var paymentState = PrimeGlobalSynthesisPaymentService.DeriveState(paidLines, approvedLines);
        List<GlobalPoolInboxStepStatusDto>? stepStatuses = null;
        Guid? suggestedStep = null;
        if (poolWf is not null && await poolWf.UsesConfigurableWorkflowAsync(ct))
        {
            stepStatuses = await poolWf.ListScopeInboxStepStatusesAsync(s, ct);
            suggestedStep = await poolWf.GetSuggestedScopeApproveStepIdAsync(s, employeeRole, ct);
        }
        return new GlobalPoolScopeSynthesisInboxItemDto
        {
            ScopeSynthesisId = s.Id,
            Period = s.Period,
            ScopeType = s.ScopeType,
            ScopeId = s.ScopeId,
            ScopeDisplayName = s.ScopeDisplayName,
            HasFile = s.ExcelContent is { Length: > 0 },
            FileName = s.FileName,
            GeneratedAt = s.GeneratedAt,
            ManagerApprovedAt = s.ManagerApprovedAt,
            RhApprovedAt = s.RhApprovedAt,
            ComptaAckAt = s.ComptaAckAt,
            PoolDistributionUnlocked = unlocked,
            PendingActionForUser = pending,
            StepStatuses = stepStatuses,
            SuggestedApproveStepId = suggestedStep,
            PaymentState = paymentState,
            PaidLines = paidLines,
            TotalLines = totalLines,
            RhDecidedLines = rhDecidedLines,
            ManagerDecidedLines = managerDecidedLines,
            ApprovedLines = approvedLines,
            RejectedLines = rejectedLines,
        };
    }
}
