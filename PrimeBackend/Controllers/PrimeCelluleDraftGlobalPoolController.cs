using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;
using PrimeBackend.Services;

namespace PrimeBackend.Controllers;

/// <summary>Fichier Excel « pool global » rattaché à un brouillon cellule (validations Manager + RH + accusé Comptabilité).</summary>
[ApiController]
[Route("api/prime/supervisor-cellule-prime-drafts")]
[Route("api/prime/supervisor-pole-prime-drafts")]
public sealed class PrimeCelluleDraftGlobalPoolController(
    PrimeDbContext? db,
    PrimeOrgScopeService org,
    GlobalPoolWorkflowService? poolWf) : ControllerBase
{
    private static bool PoolUnlocked(SupervisorCellulePrimeDraftEntity d) =>
        d.GlobalPoolManagerApprovedAt.HasValue && d.GlobalPoolRhApprovedAt.HasValue;

    private static CelluleDraftGlobalPoolStateDto MapState(SupervisorCellulePrimeDraftEntity d) => new()
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
        PoolDistributionUnlocked = PoolUnlocked(d),
    };

    private async Task<(SupervisorCellulePrimeDraftEntity? Draft, IActionResult? Error)> TryGetOwnedDraftAsync(
        Guid draftId,
        string supervisorUserId,
        CancellationToken ct)
    {
        if (db == null) return (null, StatusCode(503, new { error = "Base de données non configurée." }));
        var sup = supervisorUserId.Trim();
        var d = await db.SupervisorCellulePrimeDrafts.FirstOrDefaultAsync(x => x.Id == draftId, ct);
        if (d is null) return (null, NotFound());
        if (!string.Equals(d.SupervisorUserId, sup, StringComparison.Ordinal) ||
            !await org.SupervisorOwnsCelluleAsync(sup, d.CelluleId, ct))
            return (null, StatusCode(403, new { error = "Accès refusé pour ce brouillon." }));
        return (d, null);
    }

    private async Task<string?> RoleOfUserAsync(string userId, CancellationToken ct)
    {
        if (db == null) return null;
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId.Trim(), ct);
        return e?.Role;
    }

    [HttpGet("{draftId:guid}/global-pool")]
    public async Task<IActionResult> GetState(
        Guid draftId,
        [FromQuery] string supervisorUserId,
        CancellationToken ct)
    {
        var (d, err) = await TryGetOwnedDraftAsync(draftId, supervisorUserId, ct);
        if (err is not null) return err;
        return Ok(MapState(d!));
    }

    [HttpGet("{draftId:guid}/global-pool/excel")]
    public async Task<IActionResult> DownloadExcel(
        Guid draftId,
        [FromQuery] string supervisorUserId,
        [FromQuery] string? actingUserId,
        CancellationToken ct)
    {
        var (d, err) = await TryGetOwnedDraftAsync(draftId, supervisorUserId, ct);
        if (err is not null) return err;
        if (d!.GlobalPoolExcelContent is not { Length: > 0 })
            return NotFound(new { error = "Aucun fichier de synthèse globale disponible pour ce brouillon. Générez-le via POST …/global-pool/generate." });

        var uid = (actingUserId ?? supervisorUserId).Trim();
        var role = await RoleOfUserAsync(uid, ct);
        if (string.IsNullOrWhiteSpace(role))
            return BadRequest(new { error = "Utilisateur inconnu." });
        var legacyOk = PoolUnlocked(d!);
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

    /// <summary>
    /// Désactivé pour l’instant : le fichier partagé RH / Manager / Compta est généré automatiquement (totaux agrégés),
    /// pas importé par le superviseur. Utilisez <see cref="GenerateGlobalPoolExcel"/>.
    /// </summary>
    [HttpPut("{draftId:guid}/global-pool/excel")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public Task<IActionResult> UploadExcel(
        Guid draftId,
        [FromQuery] string supervisorUserId,
        IFormFile file,
        CancellationToken ct) =>
        Task.FromResult<IActionResult>(StatusCode(410, new
        {
            error = "L’import manuel du fichier global est désactivé. Générez la synthèse via POST …/global-pool/generate.",
        }));

    /// <summary>
    /// Génère l’Excel de synthèse (tous pôles, tous pilotes / services, totaux uniquement) pour la période du brouillon,
    /// l’enregistre sur le brouillon et réinitialise les validations Manager / RH / Compta.
    /// </summary>
    [HttpPost("{draftId:guid}/global-pool/generate")]
    public Task<IActionResult> GenerateGlobalPoolExcel(
        Guid draftId,
        [FromQuery] string supervisorUserId,
        CancellationToken ct) =>
        Task.FromResult<IActionResult>(StatusCode(410, new
        {
            error = "Génération par brouillon superviseur désactivée. Utilisez POST /api/prime/global-pool/synthesis/generate avec period, scopeType et scopeId.",
        }));

    [HttpPost("{draftId:guid}/global-pool/generate-legacy")]
    public async Task<IActionResult> GenerateGlobalPoolExcelLegacy(
        Guid draftId,
        [FromQuery] string supervisorUserId,
        CancellationToken ct)
    {
        if (db == null) return StatusCode(503, new { error = "Base de données non configurée." });
        var (d, err) = await TryGetOwnedDraftAsync(draftId, supervisorUserId, ct);
        if (err is not null) return err;

        var bytes = await PrimeGlobalRecapExcelBuilder.BuildAsync(db, d!.Period, ct);
        var now = DateTimeOffset.UtcNow;
        var safePeriod = string.IsNullOrWhiteSpace(d.Period) ? "periode" : d.Period.Trim().Replace('/', '-');
        d.GlobalPoolExcelContent = bytes;
        d.GlobalPoolFileName = $"PRIME_synthese_globale_{safePeriod}.xlsx";
        d.GlobalPoolUploadedAt = now;
        d.GlobalPoolUploadedByUserId = "generated";
        d.GlobalPoolManagerApprovedAt = null;
        d.GlobalPoolManagerApprovedByUserId = null;
        d.GlobalPoolRhApprovedAt = null;
        d.GlobalPoolRhApprovedByUserId = null;
        d.GlobalPoolComptaAckAt = null;
        d.GlobalPoolComptaAckByUserId = null;
        d.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return Ok(MapState(d));
    }

    [HttpPost("{draftId:guid}/global-pool/approve-manager")]
    public async Task<IActionResult> ApproveManager(
        Guid draftId,
        [FromQuery] string supervisorUserId,
        [FromBody] GlobalPoolActingUserRequest body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.UserId)) return BadRequest(new { error = "UserId est requis." });
        var role = await RoleOfUserAsync(body.UserId, ct);
        if (!string.Equals(role, "Manager", StringComparison.Ordinal) && !string.Equals(role, "Admin", StringComparison.Ordinal))
            return StatusCode(403, new { error = "Seul le rôle Manager (ou Admin) peut valider cette étape." });

        var (d, err) = await TryGetOwnedDraftAsync(draftId, supervisorUserId, ct);
        if (err is not null) return err;
        if (d!.GlobalPoolExcelContent is not { Length: > 0 })
            return BadRequest(new { error = "Générez d’abord le fichier de synthèse globale (POST …/global-pool/generate)." });

        var now = DateTimeOffset.UtcNow;
        d.GlobalPoolManagerApprovedAt = now;
        d.GlobalPoolManagerApprovedByUserId = body.UserId.Trim();
        d.UpdatedAt = now;
        await db!.SaveChangesAsync(ct);
        return Ok(MapState(d));
    }

    [HttpPost("{draftId:guid}/global-pool/approve-rh")]
    public async Task<IActionResult> ApproveRh(
        Guid draftId,
        [FromQuery] string supervisorUserId,
        [FromBody] GlobalPoolActingUserRequest body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.UserId)) return BadRequest(new { error = "UserId est requis." });
        var role = await RoleOfUserAsync(body.UserId, ct);
        if (!string.Equals(role, "RH", StringComparison.Ordinal) && !string.Equals(role, "Admin", StringComparison.Ordinal))
            return StatusCode(403, new { error = "Seul le rôle RH (ou Admin) peut valider cette étape." });

        var (d, err) = await TryGetOwnedDraftAsync(draftId, supervisorUserId, ct);
        if (err is not null) return err;
        if (d!.GlobalPoolExcelContent is not { Length: > 0 })
            return BadRequest(new { error = "Générez d’abord le fichier de synthèse globale (POST …/global-pool/generate)." });

        var now = DateTimeOffset.UtcNow;
        d.GlobalPoolRhApprovedAt = now;
        d.GlobalPoolRhApprovedByUserId = body.UserId.Trim();
        d.UpdatedAt = now;
        await db!.SaveChangesAsync(ct);
        return Ok(MapState(d));
    }

    [HttpPost("{draftId:guid}/global-pool/ack-compta")]
    public async Task<IActionResult> AckCompta(
        Guid draftId,
        [FromQuery] string supervisorUserId,
        [FromBody] GlobalPoolActingUserRequest body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.UserId)) return BadRequest(new { error = "UserId est requis." });
        var role = await RoleOfUserAsync(body.UserId, ct);
        if (!string.Equals(role, "Comptable", StringComparison.Ordinal) && !string.Equals(role, "Admin", StringComparison.Ordinal))
            return StatusCode(403, new { error = "Seul le rôle Comptable (ou Admin) peut accuser réception." });

        var (d, err) = await TryGetOwnedDraftAsync(draftId, supervisorUserId, ct);
        if (err is not null) return err;
        if (!PoolUnlocked(d!))
            return BadRequest(new { error = "Les validations Manager et RH doivent être complétées avant la comptabilité." });

        var now = DateTimeOffset.UtcNow;
        d!.GlobalPoolComptaAckAt = now;
        d.GlobalPoolComptaAckByUserId = body.UserId.Trim();
        d.UpdatedAt = now;
        await db!.SaveChangesAsync(ct);
        return Ok(MapState(d));
    }
}
