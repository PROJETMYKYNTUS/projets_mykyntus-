using Microsoft.EntityFrameworkCore;
using Prime.Application;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Infrastructure.Persistence;
using Prime.Infrastructure.Services;

namespace Prime.Infrastructure.Services;

public sealed class PrimeFichePreviewAppService(
    PrimeDbContext db,
    IPrimeRequestUserResolver userResolver,
    PrimeFicheMergedPreviewAccessService previewAccess) : IPrimeFichePreviewAppService
{
    private const string IdentityError =
        "Utilisateur introuvable ou identité incomplète (userId / rôle requis).";

    public async Task<MergedFichePreviewContextDto> GetMergedPreviewContextAsync(
        Guid ficheId,
        string? userId,
        string? role,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(role))
            throw new PrimeApiException(401, IdentityError);

        var ru = await userResolver.TryResolveAsync(userId, role, ct);
        if (ru is null)
        {
            var impersonated = await userResolver.TryResolveForValidationAsync(userId, role, ct);
            if (impersonated is not null &&
                string.Equals(impersonated.Employee.Role?.Trim(), "Admin", StringComparison.Ordinal))
                ru = impersonated;
            else
                throw new PrimeApiException(401, IdentityError);
        }

        var fiche = await db.EmployeePrimeServiceFiches.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == ficheId, ct)
            ?? throw new KeyNotFoundException();

        if (!await previewAccess.CanAccessMergedPreviewAsync(ru, fiche, ct))
            throw new PrimeApiException(403, "Accès refusé pour cette fiche PRIME.");

        var ctx = await previewAccess.BuildContextAsync(fiche, ct)
            ?? throw new KeyNotFoundException();
        return ctx;
    }
}
