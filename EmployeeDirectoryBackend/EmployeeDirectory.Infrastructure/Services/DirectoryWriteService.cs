using EmployeeDirectory.Application.Abstractions;
using EmployeeDirectory.Application.Dtos;
using EmployeeDirectory.Domain.Entities;
using EmployeeDirectory.Infrastructure.Data;
using Kyntus.Messaging.Contracts;
using Kyntus.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;

namespace EmployeeDirectory.Infrastructure.Services;

public sealed class DirectoryWriteService(
    DirectoryDbContext db,
    IOutboxWriter outbox,
    DirectoryHierarchyService hierarchy) : IDirectoryWriteService
{
    public async Task<EmployeeDto> CreateEmployeeAsync(CreateEmployeeRequest request, Guid? changedBy, CancellationToken ct = default)
    {
        var id = request.EmployeeId ?? Guid.NewGuid();

        var existingById = await db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (existingById is not null)
            return Map(existingById);

        if (await db.Employees.AnyAsync(e => e.Email.ToLower() == request.Email.Trim().ToLower(), ct))
            throw new InvalidOperationException($"Email déjà utilisé : {request.Email}");

        var (poleId, celluleId, serviceId) = await ResolveOrgIdsAsync(request.ServiceId, ct);
        var role = KyntusRoleNames.NormalizePlanningRole(request.Role);

        var employee = new Employee
        {
            Id = id,
            Email = request.Email.Trim(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Role = role,
            ServiceId = serviceId,
            CelluleId = celluleId,
            PoleId = poleId,
            ParentId = request.ParentId,
            HireDate = request.HireDate ?? DateTime.UtcNow,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        if (employee.ParentId is null)
            employee.ParentId = await hierarchy.ResolveDefaultParentIdAsync(employee, ct);

        db.Employees.Add(employee);
        await EnqueueEmployeeChangedAsync(employee, isDeleted: false, emitLegacyCreate: false, ct);
        await db.SaveChangesAsync(ct);
        return Map(employee);
    }

    public async Task<EmployeeDto?> UpdateEmployeeAsync(Guid id, UpdateEmployeeRequest request, Guid? changedBy, CancellationToken ct = default)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (employee is null) return null;

        var (poleId, celluleId, serviceId) = await ResolveOrgIdsAsync(request.ServiceId, ct);
        employee.FirstName = request.FirstName.Trim();
        employee.LastName = request.LastName.Trim();
        employee.Email = request.Email.Trim();
        employee.Role = KyntusRoleNames.NormalizePlanningRole(request.Role);
        employee.ServiceId = serviceId ?? employee.ServiceId;
        employee.CelluleId = celluleId ?? employee.CelluleId;
        employee.PoleId = poleId ?? employee.PoleId;
        employee.IsActive = request.IsActive;
        employee.ParentId = request.ParentId ?? employee.ParentId;
        employee.HireDate = request.HireDate ?? employee.HireDate;
        employee.UpdatedAt = DateTime.UtcNow;

        if (employee.ParentId is null)
            employee.ParentId = await hierarchy.ResolveDefaultParentIdAsync(employee, ct);

        await EnqueueEmployeeChangedAsync(employee, isDeleted: false, emitLegacyCreate: false, ct);
        await db.SaveChangesAsync(ct);
        return Map(employee);
    }

    public async Task<bool> DeleteEmployeeAsync(Guid id, Guid? changedBy, CancellationToken ct = default)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (employee is null) return false;
        employee.IsActive = false;
        employee.UpdatedAt = DateTime.UtcNow;
        await EnqueueEmployeeChangedAsync(employee, isDeleted: true, emitLegacyCreate: false, ct);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task AssignStructureRoleAsync(string kind, string nodeId, Guid employeeId, Guid? changedBy, string? reason, CancellationToken ct = default)
    {
        if (!Enum.TryParse<OrgAssignmentKind>(kind, true, out var assignmentKind))
            throw new ArgumentException($"Kind invalide : {kind}");

        var nodeLevel = assignmentKind switch
        {
            OrgAssignmentKind.ChefDeProjet => OrgNodeLevel.Pole,
            OrgAssignmentKind.Superviseur => OrgNodeLevel.Cellule,
            OrgAssignmentKind.ReferentTechnique => OrgNodeLevel.Service,
            _ => OrgNodeLevel.Service,
        };

        var existing = await db.OrgAssignments
            .Where(a => a.Kind == assignmentKind && a.NodeId == nodeId.Trim() && a.EffectiveTo == null)
            .ToListAsync(ct);

        foreach (var row in existing)
        {
            row.EffectiveTo = DateTime.UtcNow;
            row.SupersededBy = Guid.NewGuid();
            db.OrgAssignmentHistories.Add(new OrgAssignmentHistory
            {
                Id = Guid.NewGuid(),
                Kind = assignmentKind,
                NodeId = nodeId.Trim(),
                NodeLevel = nodeLevel,
                PreviousEmployeeId = row.EmployeeId,
                NewEmployeeId = employeeId,
                ChangedBy = changedBy,
                ChangeReason = reason,
                ChangedAt = DateTime.UtcNow,
            });
        }

        var assignment = new OrgAssignment
        {
            Id = Guid.NewGuid(),
            Kind = assignmentKind,
            NodeId = nodeId.Trim(),
            NodeLevel = nodeLevel,
            EmployeeId = employeeId,
            EffectiveFrom = DateTime.UtcNow,
            ChangedBy = changedBy,
            ChangeReason = reason,
        };
        db.OrgAssignments.Add(assignment);

        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new KeyNotFoundException("Employé introuvable.");
        await hierarchy.ApplyAssignmentToEmployeeAsync(employee, assignmentKind, nodeId.Trim(), ct);
        employee.UpdatedAt = DateTime.UtcNow;

        await outbox.EnqueueAsync(new DirectoryAssignmentChangedMessage
        {
            Kind = assignmentKind,
            NodeId = nodeId.Trim(),
            NodeLevel = nodeLevel,
            EmployeeId = employeeId,
            EmployeeEmail = employee.Email,
            NewRole = employee.Role,
            Removed = false,
        }, aggregateId: employeeId.ToString(), ct: ct);

        await EnqueueEmployeeChangedAsync(employee, isDeleted: false, emitLegacyCreate: false, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task ClearStructureRoleAsync(string kind, string nodeId, Guid? changedBy, string? reason, CancellationToken ct = default)
    {
        if (!Enum.TryParse<OrgAssignmentKind>(kind, true, out var assignmentKind))
            throw new ArgumentException($"Kind invalide : {kind}");

        var existing = await db.OrgAssignments
            .Where(a => a.Kind == assignmentKind && a.NodeId == nodeId.Trim() && a.EffectiveTo == null)
            .ToListAsync(ct);

        foreach (var row in existing)
        {
            row.EffectiveTo = DateTime.UtcNow;
            db.OrgAssignmentHistories.Add(new OrgAssignmentHistory
            {
                Id = Guid.NewGuid(),
                Kind = assignmentKind,
                NodeId = nodeId.Trim(),
                NodeLevel = row.NodeLevel,
                PreviousEmployeeId = row.EmployeeId,
                NewEmployeeId = null,
                ChangedBy = changedBy,
                ChangeReason = reason,
                ChangedAt = DateTime.UtcNow,
            });

            await outbox.EnqueueAsync(new DirectoryAssignmentChangedMessage
            {
                Kind = assignmentKind,
                NodeId = nodeId.Trim(),
                NodeLevel = row.NodeLevel,
                EmployeeId = row.EmployeeId,
                Removed = true,
            }, aggregateId: row.EmployeeId.ToString(), ct: ct);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<string> CreatePoleAsync(string name, CancellationToken ct = default)
    {
        var id = NewOrgId("pole");
        db.OrgPoles.Add(new OrgPole { Id = id, Name = name.Trim() });
        await outbox.EnqueueAsync(new DirectoryOrgNodeChangedMessage
        {
            NodeId = id,
            Name = name.Trim(),
            Level = OrgNodeLevel.Pole,
        }, aggregateId: id, ct: ct);
        await db.SaveChangesAsync(ct);
        return id;
    }

    public async Task<string> CreateCelluleAsync(string poleId, string name, CancellationToken ct = default)
    {
        var id = NewOrgId("cell");
        db.OrgCellules.Add(new OrgCellule { Id = id, Name = name.Trim(), PoleId = poleId.Trim() });
        await outbox.EnqueueAsync(new DirectoryOrgNodeChangedMessage
        {
            NodeId = id,
            Name = name.Trim(),
            Level = OrgNodeLevel.Cellule,
            ParentNodeId = poleId.Trim(),
        }, aggregateId: id, ct: ct);
        await db.SaveChangesAsync(ct);
        return id;
    }

    public async Task<string> CreateServiceAsync(string celluleId, string name, CancellationToken ct = default)
    {
        var id = NewOrgId("svc");
        db.OrgServices.Add(new OrgService { Id = id, Name = name.Trim(), CelluleId = celluleId.Trim() });
        await outbox.EnqueueAsync(new DirectoryOrgNodeChangedMessage
        {
            NodeId = id,
            Name = name.Trim(),
            Level = OrgNodeLevel.Service,
            ParentNodeId = celluleId.Trim(),
        }, aggregateId: id, ct: ct);
        await db.SaveChangesAsync(ct);
        return id;
    }

    public async Task<bool> RenameOrgNodeAsync(OrgNodeLevel level, string nodeId, string name, CancellationToken ct = default)
    {
        var trimmedId = nodeId.Trim();
        var trimmedName = name.Trim();
        switch (level)
        {
            case OrgNodeLevel.Pole:
            {
                var row = await db.OrgPoles.FirstOrDefaultAsync(p => p.Id == trimmedId, ct);
                if (row is null) return false;
                row.Name = trimmedName;
                break;
            }
            case OrgNodeLevel.Cellule:
            {
                var row = await db.OrgCellules.FirstOrDefaultAsync(c => c.Id == trimmedId, ct);
                if (row is null) return false;
                row.Name = trimmedName;
                break;
            }
            case OrgNodeLevel.Service:
            {
                var row = await db.OrgServices.FirstOrDefaultAsync(s => s.Id == trimmedId, ct);
                if (row is null) return false;
                row.Name = trimmedName;
                break;
            }
            default:
                return false;
        }

        await outbox.EnqueueAsync(new DirectoryOrgNodeChangedMessage
        {
            NodeId = trimmedId,
            Name = trimmedName,
            Level = level,
        }, aggregateId: trimmedId, ct: ct);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteOrgNodeAsync(OrgNodeLevel level, string nodeId, CancellationToken ct = default)
    {
        var trimmedId = nodeId.Trim();
        switch (level)
        {
            case OrgNodeLevel.Pole:
            {
                var pole = await db.OrgPoles
                    .Include(p => p.Cellules).ThenInclude(c => c.Services)
                    .FirstOrDefaultAsync(p => p.Id == trimmedId, ct);
                if (pole is null) return false;
                foreach (var cellule in pole.Cellules.ToList())
                {
                    db.OrgServices.RemoveRange(cellule.Services);
                    db.OrgCellules.Remove(cellule);
                }
                db.OrgPoles.Remove(pole);
                break;
            }
            case OrgNodeLevel.Cellule:
            {
                var cellule = await db.OrgCellules
                    .Include(c => c.Services)
                    .FirstOrDefaultAsync(c => c.Id == trimmedId, ct);
                if (cellule is null) return false;
                db.OrgServices.RemoveRange(cellule.Services);
                db.OrgCellules.Remove(cellule);
                break;
            }
            case OrgNodeLevel.Service:
            {
                var service = await db.OrgServices.FirstOrDefaultAsync(s => s.Id == trimmedId, ct);
                if (service is null) return false;
                db.OrgServices.Remove(service);
                break;
            }
            default:
                return false;
        }

        await outbox.EnqueueAsync(new DirectoryOrgNodeChangedMessage
        {
            NodeId = trimmedId,
            Level = level,
            IsDeleted = true,
        }, aggregateId: trimmedId, ct: ct);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SetAuthSubjectIdAsync(Guid employeeId, Guid authSubjectId, CancellationToken ct = default)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null) return false;
        employee.AuthSubjectId = authSubjectId;
        employee.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task EnqueueEmployeeChangedAsync(Employee employee, bool isDeleted, bool emitLegacyCreate, CancellationToken ct)
    {
        await outbox.EnqueueAsync(new DirectoryEmployeeChangedMessage
        {
            EmployeeId = employee.Id,
            Email = employee.Email,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            Role = employee.Role,
            ParentId = employee.ParentId,
            ServiceId = employee.ServiceId,
            CelluleId = employee.CelluleId,
            PoleId = employee.PoleId,
            IsActive = employee.IsActive,
            IsDeleted = isDeleted,
        }, aggregateId: employee.Id.ToString(), ct: ct);

        if (!emitLegacyCreate || isDeleted)
            return;

        await outbox.EnqueueAsync(new EmployeCreatedMessage
        {
            EmployeId = employee.Id,
            Nom = employee.LastName,
            Prenom = employee.FirstName,
            Email = employee.Email,
            Role = employee.Role,
            ManagerId = employee.ParentId ?? Guid.Empty,
            SupervisorId = employee.ParentId ?? Guid.Empty,
            ServiceId = Guid.TryParse(employee.ServiceId, out var sid) ? sid : Guid.Empty,
            ServiceNom = employee.ServiceId ?? "",
            PrimeServiceId = employee.ServiceId,
            DateEmbauche = employee.HireDate,
        }, aggregateId: employee.Id.ToString(), ct: ct);
    }

    private async Task<(string? PoleId, string? CelluleId, string? ServiceId)> ResolveOrgIdsAsync(string? serviceId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(serviceId)) return (null, null, null);
        var svc = await db.OrgServices.AsNoTracking().Include(s => s.Cellule).FirstOrDefaultAsync(s => s.Id == serviceId.Trim(), ct);
        if (svc is null) return (null, null, serviceId.Trim());
        return (svc.Cellule.PoleId, svc.CelluleId, svc.Id);
    }

    private static string NewOrgId(string prefix)
    {
        var s = Guid.NewGuid().ToString("N");
        return $"{prefix}-{s[..Math.Min(12, s.Length)]}";
    }

    private static EmployeeDto Map(Employee e) => new(
        e.Id.ToString(),
        e.FirstName,
        e.LastName,
        e.Role,
        e.ParentId?.ToString(),
        e.ServiceId,
        e.PoleId ?? "",
        e.CelluleId,
        e.Email,
        null);
}
