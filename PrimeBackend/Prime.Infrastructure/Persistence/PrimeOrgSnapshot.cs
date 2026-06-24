using Microsoft.EntityFrameworkCore;
using Prime.Infrastructure.Services;

namespace Prime.Infrastructure.Persistence;

public sealed record PrimeOrgServiceNode(string Id, string Name, string CelluleId, string PoleId);

public sealed record PrimeOrgCelluleNode(string Id, string Name, string PoleId, IReadOnlyList<PrimeOrgServiceNode> Services);

public sealed record PrimeOrgPoleNode(string Id, string Name, IReadOnlyList<PrimeOrgCelluleNode> Cellules);

/// <summary>Vue organisationnelle chargée depuis <c>prime_db</c> + résolution des rôles pivots.</summary>
public sealed class PrimeOrgSnapshot
{
    private readonly List<EmployeeEntity> _employees = [];

    public IReadOnlyList<PrimeOrgPoleNode> Poles { get; private init; } = [];
    public IReadOnlyList<EmployeeEntity> Employees => _employees;

    public string? AdminId { get; private init; }
    public string? RhId { get; private init; }
    public string? ManagerId { get; private init; }
    public string? ComptabiliteId { get; private init; }
    public string? AuditId { get; private init; }

    public static async Task<PrimeOrgSnapshot> LoadAsync(PrimeDbContext db, CancellationToken ct = default)
    {
        var poles = await db.Poles.AsNoTracking()
            .Include(p => p.Cellules)
            .ThenInclude(c => c.Services)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        var employees = await db.Employees.AsNoTracking().ToListAsync(ct);

        var poleNodes = poles.Select(p => new PrimeOrgPoleNode(
            p.Id,
            p.Name,
            p.Cellules.OrderBy(c => c.Name).Select(c => new PrimeOrgCelluleNode(
                c.Id,
                c.Name,
                p.Id,
                c.Services.OrderBy(s => s.Name).Select(s => new PrimeOrgServiceNode(s.Id, s.Name, c.Id, p.Id)).ToList()
            )).ToList()
        )).ToList();

        var snap = new PrimeOrgSnapshot
        {
            Poles = poleNodes,
            AdminId = FindRole(employees, "Admin"),
            RhId = FindRole(employees, "RH"),
            ManagerId = FindRole(employees, "Manager"),
            ComptabiliteId = FindRole(employees, "Comptabilité") ?? FindRole(employees, "Comptable"),
            AuditId = FindRole(employees, "Audit"),
        };
        snap._employees.AddRange(employees);
        return snap;
    }

    private void ReloadEmployees(IEnumerable<EmployeeEntity> employees)
    {
        _employees.Clear();
        _employees.AddRange(employees);
    }

    public string? SupervisorForCellule(string celluleId) =>
        Employees.FirstOrDefault(e => e.Role == "Superviseur" && e.CelluleId == celluleId)?.Id
        ?? Employees.FirstOrDefault(e => e.Role == "Superviseur" && e.PoleId == CellulePoleId(celluleId))?.Id;

    public string? ReferentForCellule(string celluleId, string? serviceId = null)
    {
        if (!string.IsNullOrWhiteSpace(serviceId))
        {
            var byService = Employees.FirstOrDefault(e =>
                (e.Role == "Référent technique" || e.Role == "Coach") && e.ServiceId == serviceId);
            if (byService is not null) return byService.Id;
        }

        return Employees.FirstOrDefault(e =>
            (e.Role == "Référent technique" || e.Role == "Coach") && e.CelluleId == celluleId)?.Id;
    }

    public string? ChefDeProjetForPole(string poleId) =>
        Employees.FirstOrDefault(e => (e.Role == "Chef de projet" || e.Role == "RP") && e.PoleId == poleId)?.Id
        ?? Employees.FirstOrDefault(e => e.Role == "Chef de projet" || e.Role == "RP")?.Id;

    public int PilotCountForService(string serviceId) =>
        Employees.Count(e => e.Role == "Pilote" && e.ServiceId == serviceId);

    public IEnumerable<EmployeeEntity> PilotsForCellule(string celluleId) =>
        Employees.Where(e => e.Role == "Pilote" && e.CelluleId == celluleId);

    public PrimeOrgCelluleNode? FindCellule(string celluleId) =>
        Poles.SelectMany(p => p.Cellules).FirstOrDefault(c => c.Id == celluleId);

    public PrimeOrgPoleNode? FindPole(string poleId) => Poles.FirstOrDefault(p => p.Id == poleId);

    private string? CellulePoleId(string celluleId) =>
        Poles.SelectMany(p => p.Cellules).FirstOrDefault(c => c.Id == celluleId)?.PoleId;

    private static string? FindRole(List<EmployeeEntity> employees, string role) =>
        employees.FirstOrDefault(e => e.Role == role)?.Id;

    /// <summary>Crée les rôles manquants pour une cellule (superviseur, référents, pilotes).</summary>
    public async Task EnsureCelluleStaffAsync(
        PrimeDbContext db,
        PrimeMoroccanDataFactory data,
        PrimeOrgCelluleNode cellule,
        CancellationToken ct)
    {
        if (cellule.Services.Count == 0)
            return;

        var pole = FindPole(cellule.PoleId);
        var emailDomain = PrimeMoroccanDataFactory.EmailDomainFromPoleName(pole?.Name ?? "contactcentre");
        var added = new List<EmployeeEntity>();
        var chefProjetId = ChefDeProjetForPole(cellule.PoleId);

        string? supervisorId = SupervisorForCellule(cellule.Id);
        if (supervisorId is null)
        {
            var sup = data.Person(emailDomain);
            var entity = new EmployeeEntity
            {
                Id = data.NewEnrichEmployeeId(),
                FirstName = sup.FirstName,
                LastName = sup.LastName,
                Role = "Superviseur",
                ParentId = chefProjetId,
                PoleId = cellule.PoleId,
                CelluleId = cellule.Id,
                ServiceId = cellule.Services.FirstOrDefault()?.Id,
                Email = sup.Email,
            };
            added.Add(entity);
            supervisorId = entity.Id;
            _employees.Add(entity);
        }

        foreach (var service in cellule.Services)
        {
            var referentId = ReferentForCellule(cellule.Id, service.Id);
            if (referentId is null)
            {
                var rt = data.Person(emailDomain);
                var entity = new EmployeeEntity
                {
                    Id = data.NewEnrichEmployeeId(),
                    FirstName = rt.FirstName,
                    LastName = rt.LastName,
                    Role = "Référent technique",
                    ParentId = supervisorId,
                    PoleId = cellule.PoleId,
                    CelluleId = cellule.Id,
                    ServiceId = service.Id,
                    Email = rt.Email,
                };
                added.Add(entity);
                referentId = entity.Id;
                _employees.Add(entity);
            }

            var pilotsNeeded = Math.Max(0, 3 - PilotCountForService(service.Id));
            for (var i = 0; i < pilotsNeeded; i++)
            {
                var pilot = data.Person(emailDomain);
                added.Add(new EmployeeEntity
                {
                    Id = data.NewEnrichEmployeeId(),
                    FirstName = pilot.FirstName,
                    LastName = pilot.LastName,
                    Role = "Pilote",
                    ParentId = referentId,
                    PoleId = cellule.PoleId,
                    CelluleId = cellule.Id,
                    ServiceId = service.Id,
                    Email = pilot.Email,
                });
            }
        }

        if (added.Count == 0) return;
        db.Employees.AddRange(added);
        await db.SaveChangesAsync(ct);
        ReloadEmployees(await db.Employees.AsNoTracking().ToListAsync(ct));
    }
}
