using Microsoft.EntityFrameworkCore;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Services;

public sealed class AllowanceScopeService(PrimeDbContext db)
{
    public async Task<HashSet<string>> GetDirectReportIdsAsync(string managerUserId, CancellationToken ct)
    {
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
}
