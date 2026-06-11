using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;
using PrimeBackend.Services;

namespace PrimeBackend.Controllers;

/// <summary>Synthèse globale PRIME — workflow pool configurable ou colonnes historiques.</summary>
[ApiController]
[Route("api/prime/global-pool")]
public sealed class PrimeGlobalPoolStakeholderController(
    PrimeDbContext? db,
    IPrimeRequestUserResolver? userResolver,
    GlobalPoolWorkflowService? poolWf) : ControllerBase
{
    private static bool LegacyPoolUnlocked(SupervisorCellulePrimeDraftEntity d) =>
        d.GlobalPoolManagerApprovedAt.HasValue && d.GlobalPoolRhApprovedAt.HasValue;

    private async Task<GlobalPoolInboxItemDto> MapInboxAsync(SupervisorCellulePrimeDraftEntity d, string employeeRole, CancellationToken ct)
    {
        var pending = poolWf is not null && await poolWf.UsesConfigurableWorkflowAsync(ct)
            ? await poolWf.PendingActionForUserAsync(d, employeeRole, ct)
            : employeeRole switch
            {
                "Manager" => !d.GlobalPoolManagerApprovedAt.HasValue,
                "RH" => !d.GlobalPoolRhApprovedAt.HasValue,
                "Comptable" or "Comptabilité" => LegacyPoolUnlocked(d) && !d.GlobalPoolComptaAckAt.HasValue,
                "Admin" => !d.GlobalPoolManagerApprovedAt.HasValue || !d.GlobalPoolRhApprovedAt.HasValue ||
                           (LegacyPoolUnlocked(d) && !d.GlobalPoolComptaAckAt.HasValue),
                _ => false,
            };
        var unlocked = poolWf is not null && await poolWf.UsesConfigurableWorkflowAsync(ct)
            ? await poolWf.PoolDistributionUnlockedAsync(d, ct)
            : LegacyPoolUnlocked(d);
        List<GlobalPoolInboxStepStatusDto>? stepStatuses = null;
        Guid? suggestedStep = null;
        if (poolWf is not null && await poolWf.UsesConfigurableWorkflowAsync(ct))
        {
            stepStatuses = await poolWf.ListInboxStepStatusesAsync(d, ct);
            suggestedStep = await poolWf.GetSuggestedApproveStepIdAsync(d, employeeRole, ct);
        }
        return new GlobalPoolInboxItemDto
        {
            DraftId = d.Id,
            SupervisorUserId = d.SupervisorUserId,
            CelluleId = d.CelluleId,
            Period = d.Period,
            HasFile = d.GlobalPoolExcelContent is { Length: > 0 },
            FileName = d.GlobalPoolFileName,
            UploadedAt = d.GlobalPoolUploadedAt,
            ManagerApprovedAt = d.GlobalPoolManagerApprovedAt,
            RhApprovedAt = d.GlobalPoolRhApprovedAt,
            ComptaAckAt = d.GlobalPoolComptaAckAt,
            PoolDistributionUnlocked = unlocked,
            PendingActionForUser = pending,
            StepStatuses = stepStatuses,
            SuggestedApproveStepId = suggestedStep,
        };
    }

    private static CelluleDraftGlobalPoolStateDto MapState(SupervisorCellulePrimeDraftEntity d, bool unlocked) => new()
    {
        DraftId = d.Id,
        CelluleId = d.CelluleId,
        Period = d.Period,
        HasFile = d.GlobalPoolExcelContent is { Length: > 0 },
        FileName = d.GlobalPoolFileName,
        UploadedAt = d.GlobalPoolUploadedAt,
        ManagerApprovedAt = d.GlobalPoolManagerApprovedAt,
        RhApprovedAt = d.GlobalPoolRhApprovedAt,
        ComptaAckAt = d.GlobalPoolComptaAckAt,
        PoolDistributionUnlocked = unlocked,
    };

    private async Task<string?> RoleOfUserAsync(string userId, CancellationToken ct)
    {
        if (db == null) return null;
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId.Trim(), ct);
        return e?.Role;
    }

    private async Task<(SupervisorCellulePrimeDraftEntity? Draft, IActionResult? Error)> TryGetDraftAsync(Guid draftId, CancellationToken ct)
    {
        if (db == null) return (null, StatusCode(503, new { error = "Base de données non configurée." }));
        var d = await db.SupervisorCellulePrimeDrafts.FirstOrDefaultAsync(x => x.Id == draftId, ct);
        if (d is null) return (null, NotFound());
        return (d, null);
    }

    [HttpGet("inbox")]
    public async Task<ActionResult<List<GlobalPoolInboxItemDto>>> Inbox([FromQuery] string userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId)) return BadRequest(new { error = "userId est requis." });
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });

        var role = await RoleOfUserAsync(userId, ct);
        if (string.IsNullOrWhiteSpace(role)) return BadRequest(new { error = "Utilisateur inconnu." });

        var list = await db.SupervisorCellulePrimeDrafts.AsNoTracking()
            .Where(d => d.GlobalPoolExcelContent != null && d.GlobalPoolExcelContent.Length > 0)
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync(ct);

        var allowed = role is "Manager" or "RH" or "Comptable" or "Comptabilité" or "Admin";
        if (!allowed) return StatusCode(403, new { error = "Rôle non autorisé sur la file synthèse globale." });

        var result = new List<GlobalPoolInboxItemDto>();
        foreach (var d in list)
            result.Add(await MapInboxAsync(d, role, ct));
        return Ok(result);
    }

    [HttpGet("{draftId:guid}/excel")]
    public async Task<IActionResult> DownloadExcel(Guid draftId, [FromQuery] string userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId)) return BadRequest(new { error = "userId est requis." });
        var (d, err) = await TryGetDraftAsync(draftId, ct);
        if (err is not null) return err;
        if (d!.GlobalPoolExcelContent is not { Length: > 0 })
            return NotFound(new { error = "Aucun fichier de synthèse globale pour ce brouillon." });

        var role = await RoleOfUserAsync(userId, ct);
        if (string.IsNullOrWhiteSpace(role))
            return BadRequest(new { error = "Utilisateur inconnu." });
        var legacyOk = LegacyPoolUnlocked(d!);
        var fullyUnlocked = poolWf is not null && await poolWf.UsesConfigurableWorkflowAsync(ct)
            ? await poolWf.PoolDistributionUnlockedAsync(d!, ct)
            : legacyOk;
        var allowRole = string.Equals(role, "Superviseur", StringComparison.Ordinal) ||
                        string.Equals(role, "Admin", StringComparison.Ordinal) ||
                        string.Equals(role, "RH", StringComparison.Ordinal) ||
                        string.Equals(role, "Manager", StringComparison.Ordinal) ||
                        PrimeFicheValidationRoles.IsOperationalApprover(role) ||
                        string.Equals(role, "Comptable", StringComparison.Ordinal) ||
                        string.Equals(role, "Comptabilité", StringComparison.Ordinal);
        if (!allowRole)
            return StatusCode(403, new { error = "Rôle non autorisé à télécharger le fichier global." });
        if (!PrimeFicheDistributionAccess.CanDownloadGlobalPoolSynthesis(role, legacyOk, fullyUnlocked))
            return StatusCode(403, new { error = "Fichier non diffusé : en attente des validations PRIME." });

        var name = string.IsNullOrWhiteSpace(d.GlobalPoolFileName) ? "prime-global-pool.xlsx" : d.GlobalPoolFileName.Trim();
        return File(d.GlobalPoolExcelContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    [HttpPost("{draftId:guid}/approve-step")]
    public async Task<IActionResult> ApproveStep(Guid draftId, [FromBody] GlobalPoolApproveStepRequest body, CancellationToken ct)
    {
        if (db == null || poolWf == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var ru = userResolver is null ? null : await userResolver.TryResolveAsync(Request, body.UserId, body.Role, ct);
        if (ru is null) return Unauthorized(new { error = "Utilisateur invalide." });
        var role = await RoleOfUserAsync(ru.UserId, ct);
        if (string.IsNullOrWhiteSpace(role)) return BadRequest(new { error = "Utilisateur inconnu." });

        var (d, err) = await TryGetDraftAsync(draftId, ct);
        if (err is not null) return err;
        if (!await poolWf.UsesConfigurableWorkflowAsync(ct))
            return BadRequest(new { error = "Workflow global non configuré — utilisez les routes approve-manager / approve-rh / ack-compta." });

        var (ok, msg) = await poolWf.TryApproveStepAsync(d!, body.StepId, ru.UserId, role, ct);
        if (!ok) return BadRequest(new { error = msg });
        var unlocked = await poolWf.PoolDistributionUnlockedAsync(d!, ct);
        return Ok(MapState(d!, unlocked));
    }

    [HttpPost("{draftId:guid}/approve-manager")]
    public async Task<IActionResult> ApproveManagerStakeholder(Guid draftId, [FromBody] GlobalPoolActingUserRequest body, CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (poolWf is not null && await poolWf.UsesConfigurableWorkflowAsync(ct))
            return BadRequest(new { error = "Utilisez POST .../approve-step avec l'identifiant d'étape (workflow configurable)." });
        if (string.IsNullOrWhiteSpace(body.UserId)) return BadRequest(new { error = "UserId est requis." });
        var role = await RoleOfUserAsync(body.UserId, ct);
        if (!string.Equals(role, "Manager", StringComparison.Ordinal) && !string.Equals(role, "Admin", StringComparison.Ordinal))
            return StatusCode(403, new { error = "Seul le rôle Manager (ou Admin) peut valider cette étape." });

        var (d, err) = await TryGetDraftAsync(draftId, ct);
        if (err is not null) return err;
        if (d!.GlobalPoolExcelContent is not { Length: > 0 })
            return BadRequest(new { error = "Aucun fichier de synthèse globale sur ce brouillon." });

        var now = DateTimeOffset.UtcNow;
        d.GlobalPoolManagerApprovedAt = now;
        d.GlobalPoolManagerApprovedByUserId = body.UserId.Trim();
        d.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return Ok(MapState(d, LegacyPoolUnlocked(d)));
    }

    [HttpPost("{draftId:guid}/approve-rh")]
    public async Task<IActionResult> ApproveRhStakeholder(Guid draftId, [FromBody] GlobalPoolActingUserRequest body, CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (poolWf is not null && await poolWf.UsesConfigurableWorkflowAsync(ct))
            return BadRequest(new { error = "Utilisez POST .../approve-step avec l'identifiant d'étape (workflow configurable)." });
        if (string.IsNullOrWhiteSpace(body.UserId)) return BadRequest(new { error = "UserId est requis." });
        var role = await RoleOfUserAsync(body.UserId, ct);
        if (!string.Equals(role, "RH", StringComparison.Ordinal) && !string.Equals(role, "Admin", StringComparison.Ordinal))
            return StatusCode(403, new { error = "Seul le rôle RH (ou Admin) peut valider cette étape." });

        var (d, err) = await TryGetDraftAsync(draftId, ct);
        if (err is not null) return err;
        if (d!.GlobalPoolExcelContent is not { Length: > 0 })
            return BadRequest(new { error = "Aucun fichier de synthèse globale sur ce brouillon." });

        var now = DateTimeOffset.UtcNow;
        d.GlobalPoolRhApprovedAt = now;
        d.GlobalPoolRhApprovedByUserId = body.UserId.Trim();
        d.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return Ok(MapState(d, LegacyPoolUnlocked(d)));
    }

    [HttpPost("{draftId:guid}/ack-compta")]
    public async Task<IActionResult> AckComptaStakeholder(Guid draftId, [FromBody] GlobalPoolActingUserRequest body, CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        if (poolWf is not null && await poolWf.UsesConfigurableWorkflowAsync(ct))
            return BadRequest(new { error = "Utilisez POST .../approve-step avec l'identifiant d'étape (workflow configurable)." });
        if (string.IsNullOrWhiteSpace(body.UserId)) return BadRequest(new { error = "UserId est requis." });
        var role = await RoleOfUserAsync(body.UserId, ct);
        if (!string.Equals(role, "Comptable", StringComparison.Ordinal) && !string.Equals(role, "Comptabilité", StringComparison.Ordinal) && !string.Equals(role, "Admin", StringComparison.Ordinal))
            return StatusCode(403, new { error = "Seul le rôle Comptabilité (ou Admin) peut accuser réception." });

        var (d, err) = await TryGetDraftAsync(draftId, ct);
        if (err is not null) return err;
        if (!LegacyPoolUnlocked(d!))
            return BadRequest(new { error = "Les validations Manager et RH doivent être complétées avant la comptabilité." });

        var now = DateTimeOffset.UtcNow;
        d!.GlobalPoolComptaAckAt = now;
        d.GlobalPoolComptaAckByUserId = body.UserId.Trim();
        d.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return Ok(MapState(d, LegacyPoolUnlocked(d)));
    }
}
