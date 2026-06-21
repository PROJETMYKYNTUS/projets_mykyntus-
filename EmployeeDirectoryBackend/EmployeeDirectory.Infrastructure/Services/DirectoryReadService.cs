using EmployeeDirectory.Application.Abstractions;
using EmployeeDirectory.Application.Dtos;
using EmployeeDirectory.Domain.Entities;
using EmployeeDirectory.Infrastructure.Data;
using Kyntus.Messaging.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EmployeeDirectory.Infrastructure.Services;

public sealed class DirectoryReadService(DirectoryDbContext db) : IDirectoryReadService
{
    public async Task<IReadOnlyList<EmployeeDto>> GetEmployeesAsync(string? role, string? poleId, CancellationToken ct = default)
    {
        var q = db.Employees.AsNoTracking().Where(e => e.IsActive);
        if (!string.IsNullOrWhiteSpace(role))
            q = q.Where(e => e.Role == role.Trim());
        if (!string.IsNullOrWhiteSpace(poleId))
            q = q.Where(e => e.PoleId == poleId.Trim());
        var rows = await q.OrderBy(e => e.LastName).ThenBy(e => e.FirstName).ToListAsync(ct);
        return rows.Select(MapEmployee).ToList();
    }

    public async Task<EmployeeDto?> GetEmployeeByIdAsync(Guid id, CancellationToken ct = default)
    {
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return e is null ? null : MapEmployee(e);
    }

    public async Task<bool> IsEmailUsedAsync(string email, Guid? excludeEmployeeId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var normalized = email.Trim().ToLower();
        var query = db.Employees.AsNoTracking().Where(e => e.IsActive && e.Email.ToLower() == normalized);
        if (excludeEmployeeId.HasValue)
            query = query.Where(e => e.Id != excludeEmployeeId.Value);
        return await query.AnyAsync(ct);
    }

    public async Task<OrgOverviewDto> GetOrgOverviewAsync(CancellationToken ct = default)
    {
        var poles = await db.OrgPoles.AsNoTracking()
            .Include(p => p.Cellules).ThenInclude(c => c.Services)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
        var operationalDepts = await db.BusinessDepartments.AsNoTracking()
            .Where(d => d.IsActive && d.Kind == BusinessDepartmentKind.Operational)
            .OrderBy(d => d.Name)
            .ToListAsync(ct);
        var employees = await db.Employees.AsNoTracking()
            .Include(e => e.BusinessDepartment)
            .Where(e => e.IsActive).ToListAsync(ct);
        var activeAssignments = await db.OrgAssignments.AsNoTracking()
            .Where(a => a.EffectiveTo == null)
            .ToListAsync(ct);

        var etages = poles.Select(p => new EtageNodeDto(p.Id, p.Name)).ToList();
        var services = poles.SelectMany(p => p.Cellules.Select(c => new ServiceNodeDto(c.Id, c.Name, p.Id))).ToList();
        var sousServices = poles.SelectMany(p => p.Cellules.SelectMany(c => c.Services.Select(s => new SousServiceNodeDto(s.Id, s.Name, c.Id)))).ToList();

        // Legacy : DepartmentDto = pôle (compat écrans Planning / formulaires employé)
        var departments = poles.Select(p => new DepartmentDto(
            p.Id,
            p.Name,
            p.Cellules.Select(c => new PoleDto(
                c.Id,
                c.Name,
                p.Id,
                c.Services.Select(s => new CelluleDto(
                    s.Id,
                    s.Name,
                    c.Id,
                    [new TeamDto(s.Id + "-team", s.Name, s.Id)])).ToList())).ToList())).ToList();

        static OrgPoleOverviewDto MapPole(OrgPole p) => new(
            p.Id,
            p.Name,
            p.Cellules.OrderBy(c => c.Name).Select(c => new OrgCelluleOverviewDto(
                c.Id,
                c.Name,
                c.Services.OrderBy(s => s.Name).Select(s => new OrgServiceOverviewDto(s.Id, s.Name)).ToList())).ToList());

        var polesByDept = poles
            .Where(p => p.BusinessDepartmentId.HasValue)
            .GroupBy(p => p.BusinessDepartmentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Name).Select(MapPole).ToList());

        var operationalDepartments = operationalDepts.Select(d => new OperationalDepartmentOverviewDto(
            d.Id.ToString(),
            d.Code,
            d.Name,
            d.ManagerEmployeeId?.ToString(),
            polesByDept.GetValueOrDefault(d.Id) ?? [])).ToList();

        var unassignedPoles = poles
            .Where(p => !p.BusinessDepartmentId.HasValue)
            .OrderBy(p => p.Name)
            .Select(MapPole)
            .ToList();

        var managerEtage = activeAssignments
            .Where(a => a.Kind == OrgAssignmentKind.ChefDeProjet)
            .Select(a => new ManagerEtageAssignmentDto($"m|{a.EmployeeId}|{a.NodeId}", a.EmployeeId.ToString(), a.NodeId))
            .ToList();

        var supervisorService = activeAssignments
            .Where(a => a.Kind == OrgAssignmentKind.Superviseur)
            .Select(a => new SupervisorServiceAssignmentDto($"s|{a.EmployeeId}|{a.NodeId}", a.EmployeeId.ToString(), a.NodeId, a.NodeId))
            .ToList();

        var coachSousService = activeAssignments
            .Where(a => a.Kind == OrgAssignmentKind.ReferentTechnique)
            .Select(a => new CoachSousServiceAssignmentDto($"c|{a.EmployeeId}|{a.NodeId}", a.EmployeeId.ToString(), a.NodeId, a.NodeId))
            .ToList();

        var coachPilot = activeAssignments
            .Where(a => a.Kind == OrgAssignmentKind.Pilote && a.NodeLevel == OrgNodeLevel.Service)
            .Select(a =>
            {
                var coach = employees.FirstOrDefault(e => e.Id == a.EmployeeId)?.ParentId;
                return coach is null ? null : new CoachPilotLinkDto($"cp|{coach}|{a.EmployeeId}", coach.Value.ToString(), a.EmployeeId.ToString());
            })
            .Where(x => x is not null)
            .Cast<CoachPilotLinkDto>()
            .ToList();

        return new OrgOverviewDto(
            etages,
            services,
            sousServices,
            employees.Select(MapEmployee).ToList(),
            departments,
            await GetBusinessDepartmentsAsync(ct),
            operationalDepartments,
            unassignedPoles,
            managerEtage,
            supervisorService,
            coachSousService,
            coachPilot);
    }

    public async Task<IReadOnlyList<BusinessDepartmentDto>> GetBusinessDepartmentsAsync(CancellationToken ct = default)
    {
        var rows = await db.BusinessDepartments.AsNoTracking()
            .Include(d => d.PoleAssignments)
            .OrderBy(d => d.Name)
            .ToListAsync(ct);
        var poleIdsByDept = await db.OrgPoles.AsNoTracking()
            .Where(p => p.BusinessDepartmentId != null)
            .GroupBy(p => p.BusinessDepartmentId!.Value)
            .ToDictionaryAsync(g => g.Key, g => g.Select(p => p.Id).ToList(), ct);
        return rows.Select(d =>
        {
            var poleIds = poleIdsByDept.GetValueOrDefault(d.Id) ?? [];
            if (poleIds.Count == 0)
                poleIds = d.PoleAssignments.Select(p => p.PoleId).ToList();
            return MapBusinessDepartment(d, poleIds);
        }).ToList();
    }

    public async Task<BusinessDepartmentDto?> GetBusinessDepartmentByIdAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.BusinessDepartments.AsNoTracking()
            .Include(d => d.PoleAssignments)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (row is null) return null;
        var poleIds = await db.OrgPoles.AsNoTracking()
            .Where(p => p.BusinessDepartmentId == id)
            .Select(p => p.Id)
            .ToListAsync(ct);
        if (poleIds.Count == 0)
            poleIds = row.PoleAssignments.Select(p => p.PoleId).ToList();
        return MapBusinessDepartment(row, poleIds);
    }

    public async Task<OrgAssignmentAsOfDto> GetAssignmentsAsOfAsync(DateTime asOf, CancellationToken ct = default)
    {
        var utc = asOf.Kind == DateTimeKind.Utc ? asOf : DateTime.SpecifyKind(asOf, DateTimeKind.Utc);
        var rows = await db.OrgAssignments.AsNoTracking()
            .Where(a => a.EffectiveFrom <= utc && (a.EffectiveTo == null || a.EffectiveTo > utc))
            .OrderBy(a => a.Kind)
            .ToListAsync(ct);

        return new OrgAssignmentAsOfDto(utc, rows.Select(a => new ActiveAssignmentDto(
            a.Kind.ToString(),
            a.NodeId,
            a.NodeLevel.ToString(),
            a.EmployeeId.ToString(),
            a.EffectiveFrom,
            a.EffectiveTo)).ToList());
    }

    public async Task<IReadOnlyList<AssignmentHistoryEntryDto>> GetAssignmentHistoryAsync(Guid employeeId, CancellationToken ct = default)
    {
        var rows = await db.OrgAssignmentHistories.AsNoTracking()
            .Where(h => h.PreviousEmployeeId == employeeId || h.NewEmployeeId == employeeId)
            .OrderByDescending(h => h.ChangedAt)
            .Take(200)
            .ToListAsync(ct);

        return rows.Select(h => new AssignmentHistoryEntryDto(
            h.Kind.ToString(),
            h.NodeId,
            h.PreviousEmployeeId?.ToString(),
            h.NewEmployeeId?.ToString(),
            h.ChangedAt,
            h.ChangeReason)).ToList();
    }

    public async Task<bool> IsDescendantAsync(Guid viewerId, Guid targetId, CancellationToken ct = default)
    {
        if (viewerId == targetId) return true;
        var employees = await db.Employees.AsNoTracking().Select(e => new { e.Id, e.ParentId }).ToListAsync(ct);
        var guard = new HashSet<Guid>();
        var cur = employees.FirstOrDefault(e => e.Id == targetId);
        while (cur?.ParentId is Guid parentId)
        {
            if (parentId == viewerId) return true;
            if (!guard.Add(cur.Id)) break;
            cur = employees.FirstOrDefault(e => e.Id == parentId);
        }
        return false;
    }

    public async Task<RebacSubtreeDto> GetSubtreeAsync(Guid employeeId, CancellationToken ct = default)
    {
        var all = await db.Employees.AsNoTracking().Select(e => new { e.Id, e.ParentId }).ToListAsync(ct);
        var result = new List<string>();
        var queue = new Queue<Guid>();
        queue.Enqueue(employeeId);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            foreach (var child in all.Where(e => e.ParentId == id))
            {
                result.Add(child.Id.ToString());
                queue.Enqueue(child.Id);
            }
        }
        return new RebacSubtreeDto(employeeId.ToString(), result);
    }

    public async Task<RebacManagedNodesDto> GetManagedNodesAsync(Guid employeeId, string kind, CancellationToken ct = default)
    {
        if (!Enum.TryParse<OrgAssignmentKind>(kind, true, out var k))
            k = OrgAssignmentKind.Superviseur;

        var nodes = await db.OrgAssignments.AsNoTracking()
            .Where(a => a.EmployeeId == employeeId && a.Kind == k && a.EffectiveTo == null)
            .Select(a => a.NodeId)
            .ToListAsync(ct);

        return new RebacManagedNodesDto(employeeId.ToString(), k.ToString(), nodes);
    }

    private static EmployeeDto MapEmployee(Employee e) => new(
        e.Id.ToString(),
        e.FirstName,
        e.LastName,
        e.Role,
        e.ParentId?.ToString(),
        e.ServiceId,
        e.PoleId ?? "",
        e.CelluleId,
        e.Email,
        null,
        e.BusinessDepartmentId?.ToString(),
        e.BusinessDepartment?.Kind.ToString());

    private static BusinessDepartmentDto MapBusinessDepartment(BusinessDepartment d, IReadOnlyList<string> poleIds) => new(
        d.Id.ToString(),
        d.Code,
        d.Name,
        d.Kind.ToString(),
        d.ManagerEmployeeId?.ToString(),
        d.IsActive,
        poleIds);
}
