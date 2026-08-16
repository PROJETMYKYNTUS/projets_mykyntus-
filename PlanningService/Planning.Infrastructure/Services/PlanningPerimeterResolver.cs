using Microsoft.EntityFrameworkCore;
using Planning.Domain.Entities;
using Planning.Infrastructure.Persistence;

namespace Planning.Infrastructure.Services;

/// <summary>
/// Résout le périmètre Planning (sub-services) d'un manager depuis UserManagedServices / UserSubServices.
/// Pas de repli sur SubServiceId (appartenance ≠ responsabilité).
/// </summary>
public interface IPlanningPerimeterResolver
{
    Task<HashSet<int>> GetManagedSubServiceIdsAsync(User manager, CancellationToken ct = default);
    Task<HashSet<int>> GetManagedSubServiceIdsAsync(int userId, CancellationToken ct = default);
}

public sealed class PlanningPerimeterResolver(AppDbContext db) : IPlanningPerimeterResolver
{
    public async Task<HashSet<int>> GetManagedSubServiceIdsAsync(User manager, CancellationToken ct = default)
    {
        var subServiceIds = manager.ManagedSubServices?
            .Select(s => s.SubServiceId)
            .ToList() ?? new List<int>();

        var serviceIds = manager.ManagedServices?
            .Select(s => s.ServiceId)
            .ToList() ?? new List<int>();

        // Si les navs ne sont pas chargées, recharger depuis la DB.
        if (subServiceIds.Count == 0 && serviceIds.Count == 0)
        {
            subServiceIds = await db.UserSubServices.AsNoTracking()
                .Where(x => x.UserId == manager.Id)
                .Select(x => x.SubServiceId)
                .ToListAsync(ct);
            serviceIds = await db.UserManagedServices.AsNoTracking()
                .Where(x => x.UserId == manager.Id)
                .Select(x => x.ServiceId)
                .ToListAsync(ct);
        }

        if (serviceIds.Count > 0)
        {
            var fromServices = await db.SubServices.AsNoTracking()
                .Where(ss => serviceIds.Contains(ss.ServiceId))
                .Select(ss => ss.Id)
                .ToListAsync(ct);
            subServiceIds = subServiceIds.Union(fromServices).ToList();
        }

        return subServiceIds.ToHashSet();
    }

    public async Task<HashSet<int>> GetManagedSubServiceIdsAsync(int userId, CancellationToken ct = default)
    {
        var user = await db.Users
            .Include(u => u.ManagedSubServices)
            .Include(u => u.ManagedServices)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return [];
        return await GetManagedSubServiceIdsAsync(user, ct);
    }
}
