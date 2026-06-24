using Microsoft.EntityFrameworkCore;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Domain.Entities;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Services;

public sealed class RbacAdminService(PrimeDbContext db) : IRbacAdminService
{
    private static RbacPermissionDto Map(RbacPermission e) => new()
    {
        Id = e.Id,
        Role = e.Role,
        Action = e.Action,
        Scope = e.Scope,
        IsAllowed = e.IsAllowed,
        UpdatedAt = e.UpdatedAt,
    };

    public async Task<IReadOnlyList<RbacPermissionDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await db.RbacPermissions.AsNoTracking()
            .OrderBy(p => p.Role).ThenBy(p => p.Action).ThenBy(p => p.Scope)
            .ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<RbacCatalogDto> GetCatalogAsync(CancellationToken ct = default)
    {
        var permRoles = await db.RbacPermissions.AsNoTracking().Select(p => p.Role).Distinct().ToListAsync(ct);
        var empRoles = await db.Employees.AsNoTracking().Select(e => e.Role).Distinct().ToListAsync(ct);
        var roles = permRoles.Concat(empRoles).Distinct(StringComparer.Ordinal).OrderBy(r => r).ToList();
        return new RbacCatalogDto
        {
            Actions = ["Read", "Edit", "Validate", "Configure"],
            Scopes = ["Global", "Pole", "Cellule", "Service", "Self"],
            Roles = roles,
        };
    }

    public async Task<RbacPermissionDto> UpsertAsync(UpsertRbacPermissionRequest body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(body.Role) || string.IsNullOrWhiteSpace(body.Action) || string.IsNullOrWhiteSpace(body.Scope))
            throw new ArgumentException("Role, Action et Scope sont obligatoires.");

        var role = body.Role.Trim();
        var action = body.Action.Trim();
        var scope = body.Scope.Trim();

        var row = await db.RbacPermissions.FirstOrDefaultAsync(p => p.Role == role && p.Action == action && p.Scope == scope, ct);
        var now = DateTimeOffset.UtcNow;
        if (row is null)
        {
            row = new RbacPermission
            {
                Id = Guid.NewGuid(),
                Role = role,
                Action = action,
                Scope = scope,
                IsAllowed = body.IsAllowed,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.RbacPermissions.Add(row);
        }
        else
        {
            row.IsAllowed = body.IsAllowed;
            row.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return Map(row);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.RbacPermissions.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (row is null)
            return false;
        db.RbacPermissions.Remove(row);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
