using EmployeeDirectory.Domain.Entities;
using EmployeeDirectory.Infrastructure.Data;
using Kyntus.Messaging.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EmployeeDirectory.Infrastructure.Services;

public sealed class DirectoryHierarchyService(DirectoryDbContext db)
{
    public async Task<Guid?> ResolveDefaultParentIdAsync(Employee employee, CancellationToken ct)
    {
        if (employee.BusinessDepartmentId.HasValue)
        {
            var dept = await db.BusinessDepartments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == employee.BusinessDepartmentId.Value, ct);
            if (dept?.Kind == BusinessDepartmentKind.Support && dept.ManagerEmployeeId.HasValue)
                return dept.ManagerEmployeeId;
        }

        if (!string.IsNullOrWhiteSpace(employee.ServiceId))
        {
            var referent = await FindActiveAssigneeAsync(OrgAssignmentKind.ReferentTechnique, employee.ServiceId!, ct);
            if (referent.HasValue) return referent;
        }

        if (!string.IsNullOrWhiteSpace(employee.CelluleId))
        {
            var sup = await FindActiveAssigneeAsync(OrgAssignmentKind.Superviseur, employee.CelluleId!, ct);
            if (sup.HasValue) return sup;
        }

        if (!string.IsNullOrWhiteSpace(employee.PoleId))
        {
            var cp = await FindActiveAssigneeAsync(OrgAssignmentKind.ChefDeProjet, employee.PoleId!, ct);
            if (cp.HasValue) return cp;
        }

        return null;
    }

    public async Task ApplyAssignmentToEmployeeAsync(Employee employee, OrgAssignmentKind kind, string nodeId, CancellationToken ct)
    {
        switch (kind)
        {
            case OrgAssignmentKind.ChefDeProjet:
                employee.Role = KyntusRoleNames.ChefDeProjet;
                employee.PoleId = nodeId;
                employee.CelluleId = null;
                employee.ServiceId = null;
                employee.ParentId = null;
                break;
            case OrgAssignmentKind.Superviseur:
                employee.Role = KyntusRoleNames.Superviseur;
                employee.CelluleId = nodeId;
                var cell = await db.OrgCellules.AsNoTracking().FirstOrDefaultAsync(c => c.Id == nodeId, ct);
                employee.PoleId = cell?.PoleId;
                employee.ParentId = cell?.PoleId is null
                    ? null
                    : await FindActiveAssigneeAsync(OrgAssignmentKind.ChefDeProjet, cell.PoleId, ct);
                break;
            case OrgAssignmentKind.ReferentTechnique:
                employee.Role = KyntusRoleNames.ReferentTechnique;
                employee.ServiceId = nodeId;
                var svc = await db.OrgServices.AsNoTracking().Include(s => s.Cellule).FirstOrDefaultAsync(s => s.Id == nodeId, ct);
                if (svc is not null)
                {
                    employee.CelluleId = svc.CelluleId;
                    employee.PoleId = svc.Cellule.PoleId;
                    employee.ParentId = await FindActiveAssigneeAsync(OrgAssignmentKind.Superviseur, svc.CelluleId, ct);
                }
                break;
            case OrgAssignmentKind.Pilote:
                employee.Role = KyntusRoleNames.Pilote;
                employee.ServiceId = nodeId;
                var pilotSvc = await db.OrgServices.AsNoTracking().Include(s => s.Cellule).FirstOrDefaultAsync(s => s.Id == nodeId, ct);
                if (pilotSvc is not null)
                {
                    employee.CelluleId = pilotSvc.CelluleId;
                    employee.PoleId = pilotSvc.Cellule.PoleId;
                }
                employee.ParentId = await FindActiveAssigneeAsync(OrgAssignmentKind.ReferentTechnique, nodeId, ct)
                    ?? await ResolveDefaultParentIdAsync(employee, ct);
                break;
        }
    }

    private async Task<Guid?> FindActiveAssigneeAsync(OrgAssignmentKind kind, string nodeId, CancellationToken ct)
    {
        var row = await db.OrgAssignments.AsNoTracking()
            .Where(a => a.Kind == kind && a.NodeId == nodeId && a.EffectiveTo == null)
            .OrderByDescending(a => a.EffectiveFrom)
            .FirstOrDefaultAsync(ct);
        return row?.EmployeeId;
    }
}
