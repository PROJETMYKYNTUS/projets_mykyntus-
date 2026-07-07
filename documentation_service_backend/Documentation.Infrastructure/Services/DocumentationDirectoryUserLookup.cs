using System.Security.Claims;
using Documentation.Domain.Entities;
using Documentation.Infrastructure.Persistence;
using Kyntus.Identity.Jwt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace Documentation.Infrastructure.Services;

/// <summary>Résout l'utilisateur annuaire documentation avec cache mémoire (évite N lookups DB par page).</summary>
public sealed class DocumentationDirectoryUserLookup(
    DocumentationDbContext db,
    DocumentationJwtDirectoryProvisioner jwtProvisioner,
    IMemoryCache cache,
    IConfiguration configuration)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private readonly bool _autoProvisionDirectoryFromJwt =
        configuration.GetValue("Documentation:AutoProvisionDirectoryFromJwt", false)
        || configuration.GetValue("Documentation:AllowHeaderContextFallback", false)
        || configuration.GetValue("Documentation:DemoDataSeed", false);

    public async Task<DirectoryUser?> ResolveAsync(
        ClaimsPrincipal principal,
        string tenantId,
        CancellationToken ct)
    {
        var email = principal.GetEmail()?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var cacheKey = $"doc-dir:{tenantId}:{email}";
        if (cache.TryGetValue(cacheKey, out DirectoryUser? cached) && cached is not null)
            return cached;

        var directoryUser = await db.DirectoryUsers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.TenantId == tenantId && u.Email.ToLower() == email,
                ct);

        if (directoryUser is null && _autoProvisionDirectoryFromJwt)
            directoryUser = await jwtProvisioner.TryProvisionAsync(principal, tenantId, ct);

        if (directoryUser is not null)
            cache.Set(cacheKey, directoryUser, CacheTtl);

        return directoryUser;
    }
}
