using Kyntus.Iam;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;

namespace PrimeBackend.Services;

/// <summary>Catalogue RBAC local Prime (table <c>prime_rbac_permission</c>) pour <see cref="IPolicyEvaluator"/>.</summary>
public sealed class PrimePermissionCatalog(PrimeDbContext db) : IPermissionCatalog
{
    public Task<bool> RoleHasActionAsync(string role, string action, string scope, CancellationToken ct = default) =>
        db.RbacPermissions.AsNoTracking()
            .AnyAsync(p => p.Role == role.Trim() && p.Action == action.Trim() && p.Scope == scope.Trim() && p.IsAllowed, ct);

    public async Task<IReadOnlyList<string>> GetEffectivePermissionKeysAsync(string role, CancellationToken ct = default)
    {
        var rows = await db.RbacPermissions.AsNoTracking()
            .Where(p => p.Role == role.Trim() && p.IsAllowed)
            .Select(p => p.Action + ":" + p.Scope)
            .Distinct()
            .ToListAsync(ct);
        return rows;
    }
}
