using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Models;

namespace PrimeBackend.Services;

/// <summary>
/// Périmètre organisationnel et hiérarchie issus uniquement de PostgreSQL (plus de dépendance à <see cref="PrimeInMemoryStore"/> pour les contrôleurs métier).
/// </summary>
public sealed class PrimeOrgScopeService(PrimeDbContext? db)
{
    public bool IsDatabaseAvailable => db != null;

    public async Task<string?> GetCelluleIdForServiceAsync(string serviceId, CancellationToken ct = default)
    {
        if (db == null) return null;
        var sid = serviceId.Trim();
        return await db.Services.AsNoTracking()
            .Where(s => s.Id == sid)
            .Select(s => s.CelluleId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> SupervisorOwnsCelluleAsync(string supervisorUserId, string celluleId, CancellationToken ct = default)
    {
        if (db == null) return false;
        var u = supervisorUserId.Trim();
        var c = celluleId.Trim();
        return await db.Employees.AsNoTracking()
            .AnyAsync(e => e.Id == u && e.Role == "Superviseur" && e.CelluleId == c, ct);
    }

    /// <summary>
    /// Certains clients envoient l’identifiant du <b>pôle</b> (ex. <c>d1</c>) là où l’API attend la <b>cellule</b> (ex. <c>p1</c>).
    /// Renvoie l’id cellule canonique pour un superviseur, ou <c>null</c> si le couple (superviseur, clé) est invalide.
    /// </summary>
    public async Task<string?> NormalizeSupervisorDraftCelluleKeyAsync(
        string supervisorUserId,
        string celluleIdOrLegacyPoleId,
        CancellationToken ct = default)
    {
        if (db == null) return null;
        var emp = await GetEmployeeAsync(supervisorUserId, ct);
        if (emp is null || !string.Equals(emp.Role, "Superviseur", StringComparison.OrdinalIgnoreCase))
            return null;
        var key = celluleIdOrLegacyPoleId.Trim();
        if (string.IsNullOrEmpty(key)) return null;
        if (string.Equals(emp.CelluleId, key, StringComparison.Ordinal))
            return emp.CelluleId;
        // Ancre « grand pôle » (PoleEntity.Id) au lieu de CelluleEntity.Id
        if (string.Equals(emp.PoleId, key, StringComparison.Ordinal))
            return emp.CelluleId;
        return null;
    }

    public async Task<HashSet<string>> GetSupervisedCelluleIdsAsync(string supervisorUserId, CancellationToken ct = default)
    {
        if (db == null) return new HashSet<string>(StringComparer.Ordinal);
        var u = supervisorUserId.Trim();
        var rows = await db.Employees.AsNoTracking()
            .Where(e => e.Id == u && e.Role == "Superviseur")
            .Select(e => e.CelluleId)
            .Distinct()
            .ToListAsync(ct);
        return rows.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<List<EmployeeEntity>> GetEmployeesInServiceAsync(string serviceId, CancellationToken ct = default)
    {
        if (db == null) return [];
        var sid = serviceId.Trim();
        return await db.Employees.AsNoTracking()
            .Where(e => e.ServiceId == sid)
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .ToListAsync(ct);
    }

    public async Task<EmployeeEntity?> GetEmployeeAsync(string employeeId, CancellationToken ct = default)
    {
        if (db == null) return null;
        return await db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == employeeId.Trim(), ct);
    }

    public async Task<List<(string ServiceId, string ServiceName, string CelluleId)>> GetServicesForCellulesAsync(
        IEnumerable<string> celluleIds,
        CancellationToken ct = default)
    {
        if (db == null) return [];
        var set = celluleIds.Select(x => x.Trim()).Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        return await db.Services.AsNoTracking()
            .Where(s => set.Contains(s.CelluleId))
            .OrderBy(s => s.CelluleId)
            .ThenBy(s => s.Name)
            .Select(s => new ValueTuple<string, string, string>(s.Id, s.Name, s.CelluleId))
            .ToListAsync(ct);
    }

    public async Task<int> CountEmployeesForCelluleAsync(string celluleId, CancellationToken ct = default)
    {
        if (db == null) return 0;
        var cid = celluleId.Trim();
        var serviceIds = await db.Services.AsNoTracking()
            .Where(s => s.CelluleId == cid)
            .Select(s => s.Id)
            .ToListAsync(ct);
        if (serviceIds.Count == 0) return 0;
        return await db.Employees.CountAsync(e => serviceIds.Contains(e.ServiceId), ct);
    }

    public async Task<Dictionary<string, int>> GetEmployeeCountsByCelluleAsync(
        IEnumerable<string> celluleIds,
        CancellationToken ct = default)
    {
        var dict = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var raw in celluleIds.Distinct(StringComparer.Ordinal))
        {
            var id = raw.Trim();
            dict[id] = await CountEmployeesForCelluleAsync(id, ct);
        }
        return dict;
    }

    /// <summary>Même arbre « Department → Pole → Cellule → Team » que <see cref="PrimeInMemoryStore.HydrateOrganizationFromDatabase"/> pour compat JSON Angular.</summary>
    public async Task<List<Department>> GetLegacyDepartmentTreeAsync(CancellationToken ct = default)
    {
        if (db == null) return [];
        var rows = await db.Poles
            .AsNoTracking()
            .Include(d => d.Cellules)
            .ThenInclude(p => p.Services)
            .OrderBy(d => d.Id)
            .ToListAsync(ct);
        var list = new List<Department>();
        foreach (var d in rows)
        {
            list.Add(new Department
            {
                Id = d.Id,
                Name = d.Name,
                Poles = d.Cellules.OrderBy(p => p.Id).Select(p => new Pole
                {
                    Id = p.Id,
                    Name = p.Name,
                    PoleId = d.Id,
                    Cellules = p.Services.OrderBy(s => s.Id).Select(s => new Cellule
                    {
                        Id = s.Id,
                        Name = s.Name,
                        CelluleId = p.Id,
                        Services =
                        [
                            new Team
                            {
                                Id = s.Id + "-team",
                                Name = s.Name,
                                CelluleId = p.Id,
                                ServiceId = s.Id,
                            },
                        ],
                    }).ToList(),
                }).ToList(),
            });
        }
        return list;
    }

    public async Task<List<Employee>> GetLegacyEmployeesAsync(CancellationToken ct = default)
    {
        if (db == null) return [];
        var rows = await db.Employees.AsNoTracking()
            .OrderBy(e => e.Id)
            .ToListAsync(ct);
        return rows.Select(e => new Employee
        {
            Id = e.Id,
            FirstName = e.FirstName,
            LastName = e.LastName,
            Role = e.Role,
            ParentId = e.ParentId,
            PoleId = e.PoleId,
            CelluleId = e.CelluleId,
            ServiceId = e.ServiceId,
            Email = e.Email,
            Avatar = e.Avatar,
        }).ToList();
    }

    public async Task<List<PrimeResult>> GetPrimeResultsFromFichesAsync(int take, CancellationToken ct = default)
    {
        if (db == null) return [];
        var fiches = await db.EmployeePrimeServiceFiches.AsNoTracking()
            .OrderByDescending(f => f.UpdatedAt)
            .Take(take)
            .ToListAsync(ct);
        return fiches.Select(f => new PrimeResult
        {
            Id = f.Id.ToString("N"),
            EmployeeId = f.EmployeeId,
            PrimeTypeId = "fiche-service",
            Score = (int)Math.Clamp(f.TotalAmount ?? f.PrimeAmount ?? 0, 0, int.MaxValue),
            Amount = (int)Math.Clamp(f.PrimeAmount ?? 0, 0, int.MaxValue),
            Status = f.ValidationStatus,
            Period = f.Period,
            ApprovedBy = f.LastApproverUserId,
            Date = f.UpdatedAt.ToString("yyyy-MM-dd"),
        }).ToList();
    }

    /// <summary>Statistiques tableau de bord (contrat Angular <c>prime-dashboard-standard</c>).</summary>
    public async Task<object> BuildDashboardStatsAsync(CancellationToken ct = default)
    {
        if (db == null)
        {
            return new
            {
                totalPrimesThisMonth = 0,
                budgetConsumption = 0,
                topTeams = new[] { new { name = "(aucune donnée)", amount = 0 } },
                topEmployees = new[] { new { name = "-", amount = 0 } },
                primeByDepartment = Array.Empty<object>(),
                primeEvolution = Array.Empty<object>(),
            };
        }

        var now = DateTimeOffset.UtcNow;
        var ym = $"{now:yyyy-MM}";
        var fichesMonth = await db.EmployeePrimeServiceFiches.AsNoTracking()
            .Where(f => f.Period == ym)
            .ToListAsync(ct);
        var totalAmount = (int)Math.Round(fichesMonth.Sum(f => (double)(f.TotalAmount ?? 0)));

        var byService = await (
            from f in db.EmployeePrimeServiceFiches.AsNoTracking()
            join s in db.Services.AsNoTracking() on f.ServiceId equals s.Id
            group f by new { s.Id, s.Name } into g
            select new { g.Key.Name, Sum = g.Sum(x => x.TotalAmount ?? 0) }
        ).OrderByDescending(x => x.Sum).Take(5).ToListAsync(ct);

        var byEmployee = await (
            from f in db.EmployeePrimeServiceFiches.AsNoTracking()
            join e in db.Employees.AsNoTracking() on f.EmployeeId equals e.Id
            group f by new { e.Id, e.FirstName, e.LastName } into g
            select new { Name = g.Key.FirstName + " " + g.Key.LastName, Sum = g.Sum(x => x.TotalAmount ?? 0) }
        ).OrderByDescending(x => x.Sum).Take(5).ToListAsync(ct);

        var byPole = await (
            from f in db.EmployeePrimeServiceFiches.AsNoTracking()
            join e in db.Employees.AsNoTracking() on f.EmployeeId equals e.Id
            join p in db.Poles.AsNoTracking() on e.PoleId equals p.Id
            group f by p.Name into g
            select new { name = g.Key, value = g.Sum(x => x.TotalAmount ?? 0) }
        ).ToListAsync(ct);

        var evolution = await db.EmployeePrimeServiceFiches.AsNoTracking()
            .GroupBy(f => f.Period)
            .Select(g => new { month = g.Key, amount = g.Sum(x => x.TotalAmount ?? 0) })
            .OrderBy(x => x.month)
            .Take(12)
            .ToListAsync(ct);

        var budgetCap = Math.Max(totalAmount, 1) * 2;
        var budgetPct = Math.Min(100, (int)Math.Round(100.0 * totalAmount / budgetCap));

        var topTeams = byService.Select(x => new { name = x.Name, amount = (int)Math.Round((double)x.Sum) }).ToList();
        if (topTeams.Count == 0)
            topTeams.Add(new { name = "(aucune donnée)", amount = 0 });

        var topEmployees = byEmployee.Select(x => new { name = x.Name, amount = (int)Math.Round((double)x.Sum) }).ToList();
        if (topEmployees.Count == 0)
            topEmployees.Add(new { name = "-", amount = 0 });

        return new
        {
            totalPrimesThisMonth = Math.Max(totalAmount, 0),
            budgetConsumption = budgetPct,
            topTeams,
            topEmployees,
            primeByDepartment = byPole.Select(x => new { name = x.name, value = (int)Math.Round((double)x.value) }).ToList(),
            primeEvolution = evolution.Select(x => new
            {
                month = x.month.Length >= 7 ? x.month[5..7] : x.month,
                amount = (int)Math.Round((double)x.amount),
            }).ToList(),
        };
    }
}
