using EmployeeDirectory.Domain.Entities;
using EmployeeDirectory.Infrastructure.Persistence;
using EmployeeDirectory.Infrastructure.Services;
using Kyntus.Messaging.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EmployeeDirectory.Infrastructure.Messaging;

public sealed class DirectoryEmployeSyncService(DirectoryDbContext db, DirectoryHierarchyService hierarchy)
{
    public async Task UpsertFromEmployeMessageAsync(
        Guid employeId,
        string firstName,
        string lastName,
        string email,
        string role,
        string? primeServiceId,
        Guid supervisorId,
        CancellationToken ct)
    {
        var normalizedRole = KyntusRoleNames.NormalizePlanningRole(role);
        var existing = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeId, ct)
            ?? await db.Employees.FirstOrDefaultAsync(e => e.Email.ToLower() == email.Trim().ToLower(), ct);

        var (poleId, celluleId, serviceId) = await ResolveOrgIdsAsync(primeServiceId, ct);

        if (existing is null)
        {
            existing = new Employee
            {
                Id = employeId,
                Email = email.Trim(),
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                Role = normalizedRole,
                ServiceId = serviceId,
                CelluleId = celluleId,
                PoleId = poleId,
                ParentId = supervisorId != Guid.Empty ? supervisorId : null,
                HireDate = DateTime.UtcNow,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            if (existing.ParentId is null)
                existing.ParentId = await hierarchy.ResolveDefaultParentIdAsync(existing, ct);
            db.Employees.Add(existing);
        }
        else
        {
            if (existing.Id != employeId)
            {
                db.Employees.Remove(existing);
                existing = new Employee
                {
                    Id = employeId,
                    Email = email.Trim(),
                    FirstName = firstName.Trim(),
                    LastName = lastName.Trim(),
                    Role = normalizedRole,
                    ServiceId = serviceId ?? existing.ServiceId,
                    CelluleId = celluleId ?? existing.CelluleId,
                    PoleId = poleId ?? existing.PoleId,
                    ParentId = supervisorId != Guid.Empty ? supervisorId : existing.ParentId,
                    HireDate = existing.HireDate,
                    IsActive = existing.IsActive,
                    CreatedAt = existing.CreatedAt,
                };
                db.Employees.Add(existing);
            }
            else
            {
                existing.Email = email.Trim();
                existing.FirstName = firstName.Trim();
                existing.LastName = lastName.Trim();
                if (!HasStructureRole(existing))
                    existing.Role = normalizedRole;
                if (!HasStructureRole(existing))
                {
                    existing.ServiceId = serviceId ?? existing.ServiceId;
                    existing.CelluleId = celluleId ?? existing.CelluleId;
                    existing.PoleId = poleId ?? existing.PoleId;
                }
                if (supervisorId != Guid.Empty)
                    existing.ParentId = supervisorId;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static bool HasStructureRole(Employee e) =>
        KyntusRoleNames.IsChefDeProjet(e.Role)
        || KyntusRoleNames.IsSuperviseur(e.Role)
        || KyntusRoleNames.IsReferentTechnique(e.Role);

    private async Task<(string? PoleId, string? CelluleId, string? ServiceId)> ResolveOrgIdsAsync(string? serviceId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(serviceId)) return (null, null, null);
        var svc = await db.OrgServices.AsNoTracking().Include(s => s.Cellule).FirstOrDefaultAsync(s => s.Id == serviceId.Trim(), ct);
        if (svc is null) return (null, null, serviceId.Trim());
        return (svc.Cellule.PoleId, svc.CelluleId, svc.Id);
    }
}

public sealed class DirectoryOrgSyncService(DirectoryDbContext db)
{
    public async Task UpsertNodeCreatedAsync(OrgNodeCreatedMessage msg, CancellationToken ct)
    {
        switch (msg.Level)
        {
            case OrgNodeLevel.Pole:
                if (!await db.OrgPoles.AnyAsync(p => p.Id == msg.NodeId, ct))
                    db.OrgPoles.Add(new OrgPole { Id = msg.NodeId, Name = msg.Name });
                else
                {
                    var p = await db.OrgPoles.FirstAsync(x => x.Id == msg.NodeId, ct);
                    p.Name = msg.Name;
                }
                break;
            case OrgNodeLevel.Cellule when !string.IsNullOrWhiteSpace(msg.ParentNodeId):
                if (!await db.OrgCellules.AnyAsync(c => c.Id == msg.NodeId, ct))
                    db.OrgCellules.Add(new OrgCellule { Id = msg.NodeId, Name = msg.Name, PoleId = msg.ParentNodeId! });
                else
                {
                    var c = await db.OrgCellules.FirstAsync(x => x.Id == msg.NodeId, ct);
                    c.Name = msg.Name;
                }
                break;
            case OrgNodeLevel.Service when !string.IsNullOrWhiteSpace(msg.ParentNodeId):
                if (!await db.OrgServices.AnyAsync(s => s.Id == msg.NodeId, ct))
                    db.OrgServices.Add(new OrgService { Id = msg.NodeId, Name = msg.Name, CelluleId = msg.ParentNodeId! });
                else
                {
                    var s = await db.OrgServices.FirstAsync(x => x.Id == msg.NodeId, ct);
                    s.Name = msg.Name;
                }
                break;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task RenameNodeAsync(OrgNodeRenamedMessage msg, CancellationToken ct)
    {
        switch (msg.Level)
        {
            case OrgNodeLevel.Pole:
                var p = await db.OrgPoles.FirstOrDefaultAsync(x => x.Id == msg.NodeId, ct);
                if (p is not null) p.Name = msg.NewName;
                break;
            case OrgNodeLevel.Cellule:
                var c = await db.OrgCellules.FirstOrDefaultAsync(x => x.Id == msg.NodeId, ct);
                if (c is not null) c.Name = msg.NewName;
                break;
            case OrgNodeLevel.Service:
                var s = await db.OrgServices.FirstOrDefaultAsync(x => x.Id == msg.NodeId, ct);
                if (s is not null) s.Name = msg.NewName;
                break;
        }
        await db.SaveChangesAsync(ct);
    }
}

public sealed class DirectoryAssignmentSyncService(
    DirectoryDbContext db,
    DirectoryHierarchyService hierarchy)
{
    public async Task ApplyOrgAssignmentAsync(OrgAssignmentChangedMessage msg, CancellationToken ct)
    {
        if (!Guid.TryParse(msg.EmployeeId, out var employeeId)) return;

        if (msg.Removed)
        {
            var msgKind = MessagingEnumMapper.FromMessage(msg.Kind);
            var active = await db.OrgAssignments
                .Where(a => a.Kind == msgKind && a.NodeId == msg.NodeId && a.EffectiveTo == null)
                .ToListAsync(ct);
            foreach (var row in active)
                row.EffectiveTo = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null) return;

        var msgKindAssign = MessagingEnumMapper.FromMessage(msg.Kind);
        var duplicate = await db.OrgAssignments.AnyAsync(
            a => a.Kind == msgKindAssign
                 && a.NodeId == msg.NodeId
                 && a.EmployeeId == employeeId
                 && a.EffectiveTo == null,
            ct);
        if (duplicate)
            return;

        // Unicité par nœud : clôturer les titulaires d'un autre employé avant l'ajout.
        var otherIncumbents = await db.OrgAssignments
            .Where(a => a.Kind == msgKindAssign
                        && a.NodeId == msg.NodeId
                        && a.EmployeeId != employeeId
                        && a.EffectiveTo == null)
            .ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var row in otherIncumbents)
            row.EffectiveTo = now;

        db.OrgAssignments.Add(new OrgAssignment
        {
            Id = Guid.NewGuid(),
            Kind = msgKindAssign,
            NodeId = msg.NodeId,
            NodeLevel = MessagingEnumMapper.FromMessage(msg.NodeLevel),
            EmployeeId = employeeId,
            EffectiveFrom = now,
            ChangeReason = "sync-from-prime",
        });

        await hierarchy.ApplyAssignmentToEmployeeAsync(employee, msgKindAssign, msg.NodeId, ct);
        employee.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
    }
}
