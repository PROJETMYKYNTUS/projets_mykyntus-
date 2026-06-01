using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;
using PrimeBackend.Services;

namespace PrimeBackend.Controllers;

/// <summary>Aperçu fusionné fiche PRIME (validateurs W1, RH/Manager W2).</summary>
[ApiController]
[Route("api/prime/fiches")]
public sealed class PrimeFichePreviewController(
    PrimeDbContext? db,
    IPrimeRequestUserResolver? userResolver,
    PrimeFicheMergedPreviewAccessService? previewAccess) : ControllerBase
{
    private const string IdentityError =
        "Utilisateur introuvable ou identité incomplète (userId / rôle requis).";

    [HttpGet("{ficheId:guid}/merged-preview-context")]
    public async Task<ActionResult<MergedFichePreviewContextDto>> MergedPreviewContext(
        Guid ficheId,
        [FromQuery] string? userId,
        [FromQuery] string? role,
        CancellationToken ct)
    {
        if (db is null || previewAccess is null || userResolver is null)
            return StatusCode(503, new { error = "Base de données non configurée." });

        if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(role))
            return Unauthorized(new { error = IdentityError });

        var ru = await userResolver.TryResolveAsync(Request, userId, role, ct);
        if (ru is null)
        {
            // Le rôle déclaré ne correspond pas au rôle réel : autoriser uniquement un Admin
            // à agir sous le rôle demandé (bascule de rôle développeur / impersonation).
            var impersonated = await userResolver.TryResolveForValidationAsync(Request, userId, role, ct);
            if (impersonated is not null &&
                string.Equals(impersonated.Employee.Role?.Trim(), "Admin", StringComparison.Ordinal))
                ru = impersonated;
            else
                return Unauthorized(new { error = IdentityError });
        }

        var fiche = await db.EmployeePrimeServiceFiches.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == ficheId, ct);
        if (fiche is null) return NotFound();

        if (!await previewAccess.CanAccessMergedPreviewAsync(ru, fiche, ct))
            return StatusCode(403, new { error = "Accès refusé pour cette fiche PRIME." });

        var ctx = await previewAccess.BuildContextAsync(fiche, ct);
        if (ctx is null) return NotFound();
        return Ok(ctx);
    }
}
