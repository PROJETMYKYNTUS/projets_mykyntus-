using Microsoft.EntityFrameworkCore;
using PlanningService.Data;
using PlanningService.Models;

namespace PlanningService.Services.EmployeeImport;

public sealed record OrgHierarchyRow(
    int SubServiceId,
    string SubServiceName,
    int ServiceId,
    string ServiceName,
    int FloorId,
    string FloorName);

public sealed class EmployeeImportOrgSnapshot
{
    public IReadOnlyList<OrgHierarchyRow> Rows { get; init; } = [];
    public IReadOnlyList<Role> Roles { get; init; } = [];
}

public interface IEmployeeImportOrgResolver
{
    Task<EmployeeImportOrgSnapshot> LoadSnapshotAsync(CancellationToken ct = default);
    int? ResolveSubServiceId(EmployeeImportOrgSnapshot snapshot, Dictionary<string, string?> mapped);
    void EnsurePoleExists(EmployeeImportOrgSnapshot snapshot, string? poleName);
    void EnsureCelluleExists(EmployeeImportOrgSnapshot snapshot, string? poleName, string? celluleName);
}

public class EmployeeImportOrgResolver(AppDbContext db) : IEmployeeImportOrgResolver
{
    public async Task<EmployeeImportOrgSnapshot> LoadSnapshotAsync(CancellationToken ct = default)
    {
        var floors = await db.Floors
            .AsNoTracking()
            .Include(f => f.Services)
            .ThenInclude(s => s.SubServices)
            .OrderBy(f => f.Name)
            .ToListAsync(ct);

        var rows = new List<OrgHierarchyRow>();
        foreach (var floor in floors)
        {
            foreach (var service in floor.Services.OrderBy(s => s.Name))
            {
                foreach (var sub in service.SubServices.OrderBy(s => s.Name))
                {
                    rows.Add(new OrgHierarchyRow(
                        sub.Id,
                        sub.Name,
                        service.Id,
                        service.Name,
                        floor.Id,
                        floor.Name));
                }
            }
        }

        var roles = await db.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct);
        return new EmployeeImportOrgSnapshot { Rows = rows, Roles = roles };
    }

    public int? ResolveSubServiceId(EmployeeImportOrgSnapshot snapshot, Dictionary<string, string?> mapped)
    {
        mapped.TryGetValue("pole", out var pole);
        mapped.TryGetValue("cellule", out var cellule);
        mapped.TryGetValue("service", out var service);
        mapped.TryGetValue("subService", out var legacy);

        var serviceName = !string.IsNullOrWhiteSpace(service) ? service : legacy;
        if (string.IsNullOrWhiteSpace(pole) && string.IsNullOrWhiteSpace(cellule) && string.IsNullOrWhiteSpace(serviceName))
            return null;

        var matches = FilterRows(snapshot.Rows, pole, cellule, serviceName);
        if (matches.Count == 1)
            return matches[0].SubServiceId;

        if (matches.Count > 1)
            throw new InvalidOperationException(
                $"Plusieurs services correspondent à Pôle « {pole} », Cellule « {cellule} », Service « {serviceName} ». Précisez les trois colonnes.");

        var hasPoleOrCellule = !string.IsNullOrWhiteSpace(pole) || !string.IsNullOrWhiteSpace(cellule);
        if (!string.IsNullOrWhiteSpace(serviceName) && !hasPoleOrCellule)
        {
            var norm = Normalize(serviceName);
            var byName = snapshot.Rows
                .Where(r => Normalize(r.SubServiceName) == norm)
                .ToList();
            if (byName.Count == 1)
                return byName[0].SubServiceId;
            if (byName.Count > 1)
                throw new InvalidOperationException($"Le service « {serviceName} » est ambigu — indiquez aussi le Pôle et la Cellule.");
        }

        if (int.TryParse(serviceName?.Trim(), out var id) && snapshot.Rows.Any(r => r.SubServiceId == id))
            return id;

        throw new InvalidOperationException(
            $"Organisation introuvable : Pôle « {pole} », Cellule « {cellule} », Service « {serviceName} ».");
    }

    public void EnsurePoleExists(EmployeeImportOrgSnapshot snapshot, string? poleName)
    {
        if (string.IsNullOrWhiteSpace(poleName))
            return;

        if (!snapshot.Rows.Any(r => Normalize(r.FloorName) == Normalize(poleName)))
            throw new InvalidOperationException($"Pôle introuvable : « {poleName} ».");
    }

    public void EnsureCelluleExists(EmployeeImportOrgSnapshot snapshot, string? poleName, string? celluleName)
    {
        if (string.IsNullOrWhiteSpace(celluleName))
            return;

        if (FilterRows(snapshot.Rows, poleName, celluleName, null).Count == 0)
            throw new InvalidOperationException($"Cellule introuvable : « {celluleName} ».");
    }

    private static List<OrgHierarchyRow> FilterRows(
        IReadOnlyList<OrgHierarchyRow> rows,
        string? pole,
        string? cellule,
        string? service)
    {
        IEnumerable<OrgHierarchyRow> q = rows;

        if (!string.IsNullOrWhiteSpace(pole))
            q = q.Where(r => Normalize(r.FloorName) == Normalize(pole));

        if (!string.IsNullOrWhiteSpace(cellule))
            q = q.Where(r => Normalize(r.ServiceName) == Normalize(cellule));

        if (!string.IsNullOrWhiteSpace(service))
            q = q.Where(r => Normalize(r.SubServiceName) == Normalize(service));

        return q.ToList();
    }

    private static string Normalize(string value) =>
        EmployeeImportColumnMatcher.Normalize(value);
}
