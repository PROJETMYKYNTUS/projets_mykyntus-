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
        var supervised = await GetSupervisedCelluleIdsAsync(supervisorUserId, ct);
        return supervised.Contains(celluleId.Trim());
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

    /// <summary>
    /// Cellules RH du périmètre superviseur : toutes les cellules des pôles d’affectation (<see cref="EmployeeEntity.PoleId"/>).
    /// </summary>
    public async Task<HashSet<string>> GetSupervisedCelluleIdsAsync(string supervisorUserId, CancellationToken ct = default)
    {
        if (db == null) return new HashSet<string>(StringComparer.Ordinal);
        var u = supervisorUserId.Trim();
        var emp = await db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == u && e.Role == "Superviseur", ct);
        if (emp is null) return new HashSet<string>(StringComparer.Ordinal);

        var poleIds = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(emp.PoleId))
            poleIds.Add(emp.PoleId.Trim());

        if (poleIds.Count == 0 && !string.IsNullOrWhiteSpace(emp.CelluleId))
        {
            var poleFromCell = await db.Cellules.AsNoTracking()
                .Where(c => c.Id == emp.CelluleId.Trim())
                .Select(c => c.PoleId)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(poleFromCell))
                poleIds.Add(poleFromCell.Trim());
        }

        if (poleIds.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(emp.CelluleId))
                return new HashSet<string>(StringComparer.Ordinal) { emp.CelluleId.Trim() };
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var celluleIds = await db.Cellules.AsNoTracking()
            .Where(c => poleIds.Contains(c.PoleId))
            .Select(c => c.Id)
            .ToListAsync(ct);
        return celluleIds.ToHashSet(StringComparer.Ordinal);
    }

    public static bool IsPilotRole(string? role) =>
        string.Equals(role?.Trim(), "Pilote", StringComparison.OrdinalIgnoreCase);

    public static bool IsReferentTechniqueRole(string? role)
    {
        var r = role?.Trim() ?? "";
        return string.Equals(r, "Référent technique", StringComparison.Ordinal) ||
               string.Equals(r, "Coach", StringComparison.Ordinal);
    }

    /// <summary>
    /// Périmètre validation RT : pilote direct (ParentId) ou pilote d’un RT du même superviseur / même cellule.
    /// </summary>
    public async Task<bool> IsPilotInReferentValidationScopeAsync(
        string referentUserId,
        string pilotEmployeeId,
        CancellationToken ct = default)
    {
        if (db == null) return false;
        var referent = await GetEmployeeAsync(referentUserId, ct);
        var pilote = await GetEmployeeAsync(pilotEmployeeId, ct);
        if (referent is null || pilote is null || !IsPilotRole(pilote.Role)) return false;
        if (!IsReferentTechniqueRole(referent.Role)) return false;

        if (string.Equals(pilote.ParentId, referent.Id, StringComparison.Ordinal))
            return true;

        if (!string.IsNullOrWhiteSpace(referent.ParentId))
        {
            var supervisorId = referent.ParentId.Trim();
            var referentIds = await db.Employees.AsNoTracking()
                .Where(e =>
                    e.ParentId == supervisorId &&
                    (e.Role == "Référent technique" || e.Role == "Coach"))
                .Select(e => e.Id)
                .ToListAsync(ct);
            referentIds.Add(referent.Id);
            if (!string.IsNullOrWhiteSpace(pilote.ParentId) &&
                referentIds.Contains(pilote.ParentId, StringComparer.Ordinal))
                return true;
        }

        if (!string.IsNullOrWhiteSpace(referent.CelluleId) &&
            string.Equals(pilote.CelluleId, referent.CelluleId, StringComparison.Ordinal))
            return true;

        return false;
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

    /// <summary>Employés en rôle Pilote pour un service (fiches PRIME partie personnalisée).</summary>
    public async Task<List<EmployeeEntity>> GetPilotsInServiceAsync(string serviceId, CancellationToken ct = default)
    {
        if (db == null) return [];
        var sid = serviceId.Trim();
        return await db.Employees.AsNoTracking()
            .Where(e => e.ServiceId == sid && e.Role == "Pilote")
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .ToListAsync(ct);
    }

    public async Task<List<EmployeeEntity>> GetPilotsInCelluleAsync(string celluleId, CancellationToken ct = default)
    {
        if (db == null) return [];
        var cid = celluleId.Trim();
        var serviceIds = await db.Services.AsNoTracking()
            .Where(s => s.CelluleId == cid)
            .Select(s => s.Id)
            .ToListAsync(ct);
        if (serviceIds.Count == 0) return [];
        return await db.Employees.AsNoTracking()
            .Where(e => serviceIds.Contains(e.ServiceId) && e.Role == "Pilote")
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
        return await db.Employees.CountAsync(e => serviceIds.Contains(e.ServiceId) && e.Role == "Pilote", ct);
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

    private static string NewOrgHierarchyChildId(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    /// <summary>
    /// Garantit qu’un pôle racine (<c>prime_pole</c>) possède au moins une cellule et un service feuille,
    /// nécessaire pour l’affectation « chef de projet » (ancre hiérarchique dans <see cref="PrimeInMemoryStore"/>).
    /// </summary>
    public async Task EnsureRootPoleHasMinimalChildrenAsync(string rootPoleId, CancellationToken ct = default)
    {
        if (db == null) return;
        var key = rootPoleId.Trim();
        var pole = await db.Poles
            .Include(p => p.Cellules)
            .ThenInclude(c => c.Services)
            .FirstOrDefaultAsync(p => p.Id == key, ct);
        if (pole is null) return;

        if (pole.Cellules.Any(c => c.Services.Count > 0))
            return;

        CelluleEntity targetCell;
        if (pole.Cellules.Count == 0)
        {
            var cellId = NewOrgHierarchyChildId("p");
            while (await db.Cellules.AnyAsync(c => c.Id == cellId, ct))
                cellId = NewOrgHierarchyChildId("p");
            targetCell = new CelluleEntity { Id = cellId, Name = "Cellule principale", PoleId = pole.Id };
            db.Cellules.Add(targetCell);
        }
        else
            targetCell = pole.Cellules.OrderBy(c => c.Id).First();

        var srvId = NewOrgHierarchyChildId("c");
        while (await db.Services.AnyAsync(s => s.Id == srvId, ct))
            srvId = NewOrgHierarchyChildId("c");
        db.Services.Add(new ServiceEntity { Id = srvId, Name = "Service principal", CelluleId = targetCell.Id });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Résout le pôle racine EF (<c>prime_pole</c>) à partir d’un id cellule (<c>prime_cellule</c>).</summary>
    public async Task<string?> ResolveRootPoleIdForCelluleAsync(string celluleId, CancellationToken ct = default)
    {
        if (db == null) return null;
        var key = celluleId.Trim();
        var fromCellule = await db.Cellules.AsNoTracking()
            .Where(c => c.Id == key)
            .Select(c => c.PoleId)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(fromCellule))
            return fromCellule;
        var isRootPole = await db.Poles.AsNoTracking().AnyAsync(p => p.Id == key, ct);
        return isRootPole ? key : null;
    }

    /// <summary>Pôles supervisés (RH) avec cellules et services — pour indicateurs et filtres UI.</summary>
    public async Task<List<SupervisorOrgScopePoleDto>> GetSupervisorOrganizationalScopeAsync(
        string supervisorUserId,
        CancellationToken ct = default)
    {
        if (db == null) return [];
        var celluleIds = await GetSupervisedCelluleIdsAsync(supervisorUserId, ct);
        if (celluleIds.Count == 0) return [];

        var cellules = await db.Cellules.AsNoTracking()
            .Where(c => celluleIds.Contains(c.Id))
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        var services = await db.Services.AsNoTracking()
            .Where(s => celluleIds.Contains(s.CelluleId))
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        var poleIds = cellules.Select(c => c.PoleId).Distinct(StringComparer.Ordinal).ToList();
        var poles = await db.Poles.AsNoTracking()
            .Where(p => poleIds.Contains(p.Id))
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        return poles.Select(p => new SupervisorOrgScopePoleDto
        {
            Id = p.Id,
            Name = p.Name,
            Cellules = cellules
                .Where(c => c.PoleId == p.Id)
                .Select(c => new SupervisorOrgScopeCelluleDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    RootPoleId = p.Id,
                    Services = services
                        .Where(s => s.CelluleId == c.Id)
                        .Select(s => new SupervisorOrgScopeServiceDto { Id = s.Id, Name = s.Name })
                        .ToList(),
                })
                .ToList(),
        }).ToList();
    }
}

public sealed class SupervisorOrgScopePoleDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<SupervisorOrgScopeCelluleDto> Cellules { get; set; } = [];
}

public sealed class SupervisorOrgScopeCelluleDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string RootPoleId { get; set; } = "";
    public List<SupervisorOrgScopeServiceDto> Services { get; set; } = [];
}

public sealed class SupervisorOrgScopeServiceDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}
