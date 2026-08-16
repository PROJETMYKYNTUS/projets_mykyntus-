using Kyntus.Iam;
using Microsoft.EntityFrameworkCore;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Services;

public sealed class AllowanceScopeService(PrimeDbContext db, IRebacClient? rebac = null)
{
    public async Task<HashSet<string>> GetDirectReportIdsAsync(string managerUserId, CancellationToken ct)
    {
        var fromRebac = await TryGetManagedEmployeeIdsAsync(managerUserId, ct);
        if (fromRebac is not null)
            return fromRebac;

        var ids = await db.Employees.AsNoTracking()
            .Where(e => e.ParentId == managerUserId)
            .Select(e => e.Id)
            .ToListAsync(ct);
        return ids.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<bool> IsEmployeeInSupportDepartmentAsync(string employeeId, CancellationToken ct)
    {
        var emp = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        return emp is not null
               && string.Equals(emp.BusinessDepartmentKind, "Support", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> CanManagerAccessEmployeeAsync(string managerUserId, string employeeId, CancellationToken ct)
    {
        var emp = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (emp is null) return false;
        if (!string.Equals(emp.BusinessDepartmentKind, "Support", StringComparison.OrdinalIgnoreCase))
            return false;

        var canAct = await TryCanActOnAsync(managerUserId, employeeId, ct);
        if (canAct is not null)
            return canAct.Value;

        return string.Equals(emp.ParentId, managerUserId, StringComparison.Ordinal);
    }

    public async Task<string?> GetManagerDepartmentIdAsync(string managerUserId, CancellationToken ct)
    {
        var dept = await db.BusinessDepartments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.ManagerEmployeeId == managerUserId && d.IsActive, ct);
        return dept?.Id;
    }

    public async Task<bool> IsSupportDepartmentManagerAsync(string userId, CancellationToken ct)
    {
        return await db.BusinessDepartments.AsNoTracking()
            .AnyAsync(d => d.ManagerEmployeeId == userId && d.Kind == "Support" && d.IsActive, ct);
    }

    /// <summary>
    /// Employés gérés via ReBAC ; <c>null</c> si ReBAC indisponible / échec (repli ParentId).
    /// Un ensemble vide signifie « ReBAC OK, aucun managé ».
    /// </summary>
    private async Task<HashSet<string>?> TryGetManagedEmployeeIdsAsync(string actorId, CancellationToken ct)
    {
        if (rebac is null || !Guid.TryParse(actorId, out var guid))
            return null;
        try
        {
            var ids = await rebac.GetManagedEmployeeIdsAsync(guid, ct);
            return ids
                .Select(g => g.ToString("D"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return null;
        }
    }

    /// <summary><c>null</c> si ReBAC indisponible / échec ; sinon résultat de <see cref="IRebacClient.CanActOnAsync"/>.</summary>
    private async Task<bool?> TryCanActOnAsync(string actorId, string targetEmployeeId, CancellationToken ct)
    {
        if (rebac is null
            || !Guid.TryParse(actorId, out var actor)
            || !Guid.TryParse(targetEmployeeId, out var target))
            return null;
        try
        {
            return await rebac.CanActOnAsync(actor, target, ct);
        }
        catch
        {
            return null;
        }
    }
}
