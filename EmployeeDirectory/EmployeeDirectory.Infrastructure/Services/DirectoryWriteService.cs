using EmployeeDirectory.Application.Abstractions;
using EmployeeDirectory.Application.Dtos;
using EmployeeDirectory.Domain.Entities;
using EmployeeDirectory.Domain.Enums;
using EmployeeDirectory.Infrastructure.Messaging;
using EmployeeDirectory.Infrastructure.Persistence;
using Kyntus.Messaging.Contracts;
using Kyntus.Messaging.Outbox;
using DomainAssignmentKind = EmployeeDirectory.Domain.Enums.OrgAssignmentKind;
using DomainNodeLevel = EmployeeDirectory.Domain.Enums.OrgNodeLevel;
using Microsoft.EntityFrameworkCore;

namespace EmployeeDirectory.Infrastructure.Services;

public sealed class DirectoryWriteService(
    DirectoryDbContext db,
    IOutboxWriter outbox,
    DirectoryHierarchyService hierarchy,
    IOrgStructuralRoleExclusivityService exclusivity) : IDirectoryWriteService
{
    public async Task<EmployeeDto> CreateEmployeeAsync(CreateEmployeeRequest request, Guid? changedBy, CancellationToken ct = default)
    {
        var id = request.EmployeeId ?? Guid.NewGuid();

        var existingById = await db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (existingById is not null)
            return Map(existingById);

        if (await db.Employees.AnyAsync(e => e.Email.ToLower() == request.Email.Trim().ToLower() && e.IsActive, ct))
            throw new InvalidOperationException($"Email déjà utilisé : {request.Email}");

        var inactiveByEmail = await db.Employees.FirstOrDefaultAsync(
            e => e.Email.ToLower() == request.Email.Trim().ToLower() && !e.IsActive,
            ct);
        if (inactiveByEmail is not null)
            return await ReactivateEmployeeAsync(inactiveByEmail, request, ct);

        var (poleId, celluleId, serviceId) = await ResolveOrgIdsAsync(request.ServiceId, ct);
        var role = KyntusRoleNames.NormalizePlanningRole(request.Role);
        var dept = await ResolveBusinessDepartmentAsync(request.BusinessDepartmentId, ct);

        var employee = new Employee
        {
            Id = id,
            Email = request.Email.Trim(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Role = role,
            BusinessDepartmentId = request.BusinessDepartmentId,
            ServiceId = dept?.Kind == BusinessDepartmentKind.Support ? null : serviceId,
            CelluleId = dept?.Kind == BusinessDepartmentKind.Support ? null : celluleId,
            PoleId = dept?.Kind == BusinessDepartmentKind.Support ? null : poleId,
            ParentId = request.ParentId,
            HireDate = request.HireDate ?? DateTime.UtcNow,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        DirectoryHrProfileHelper.ApplyManagers(
            employee, request.ChefDeProjetId, request.SuperviseurId, request.ReferentTechniqueId);

        if (employee.ParentId is null)
            employee.ParentId = await hierarchy.ResolveDefaultParentIdAsync(employee, ct);

        db.Employees.Add(employee);
        await DirectoryHrProfileHelper.UpsertAsync(
            db, outbox, employee.Id, request.HrProfile, employee.HireDate, ct);
        await EnqueueEmployeeChangedAsync(employee, isDeleted: false, emitLegacyCreate: false, ct);
        await db.SaveChangesAsync(ct);
        return Map(employee);
    }

    private async Task<EmployeeDto> ReactivateEmployeeAsync(
        Employee employee,
        CreateEmployeeRequest request,
        CancellationToken ct)
    {
        var (poleId, celluleId, serviceId) = await ResolveOrgIdsAsync(request.ServiceId, ct);
        var role = KyntusRoleNames.NormalizePlanningRole(request.Role);
        var dept = await ResolveBusinessDepartmentAsync(request.BusinessDepartmentId, ct);

        employee.FirstName = request.FirstName.Trim();
        employee.LastName = request.LastName.Trim();
        employee.Email = request.Email.Trim();
        employee.Role = role;
        employee.BusinessDepartmentId = request.BusinessDepartmentId;
        employee.ServiceId = dept?.Kind == BusinessDepartmentKind.Support ? null : serviceId;
        employee.CelluleId = dept?.Kind == BusinessDepartmentKind.Support ? null : celluleId;
        employee.PoleId = dept?.Kind == BusinessDepartmentKind.Support ? null : poleId;
        employee.ParentId = request.ParentId;
        employee.HireDate = request.HireDate ?? employee.HireDate;
        employee.IsActive = true;
        employee.UpdatedAt = DateTime.UtcNow;

        DirectoryHrProfileHelper.ApplyManagers(
            employee, request.ChefDeProjetId, request.SuperviseurId, request.ReferentTechniqueId);

        if (employee.ParentId is null)
            employee.ParentId = await hierarchy.ResolveDefaultParentIdAsync(employee, ct);

        await DirectoryHrProfileHelper.UpsertAsync(
            db, outbox, employee.Id, request.HrProfile, employee.HireDate, ct);
        await EnqueueEmployeeChangedAsync(employee, isDeleted: false, emitLegacyCreate: false, ct);
        await db.SaveChangesAsync(ct);
        return Map(employee);
    }

    public async Task<EmployeeDto?> UpdateEmployeeAsync(Guid id, UpdateEmployeeRequest request, Guid? changedBy, CancellationToken ct = default)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (employee is null) return null;

        var (poleId, celluleId, serviceId) = await ResolveOrgIdsAsync(request.ServiceId, ct);
        var dept = await ResolveBusinessDepartmentAsync(request.BusinessDepartmentId ?? employee.BusinessDepartmentId, ct);
        employee.FirstName = request.FirstName.Trim();
        employee.LastName = request.LastName.Trim();
        employee.Email = request.Email.Trim();
        employee.Role = KyntusRoleNames.NormalizePlanningRole(request.Role);
        employee.BusinessDepartmentId = request.BusinessDepartmentId ?? employee.BusinessDepartmentId;
        if (dept?.Kind == BusinessDepartmentKind.Support)
        {
            employee.ServiceId = null;
            employee.CelluleId = null;
            employee.PoleId = null;
        }
        else
        {
            employee.ServiceId = serviceId ?? employee.ServiceId;
            employee.CelluleId = celluleId ?? employee.CelluleId;
            employee.PoleId = poleId ?? employee.PoleId;
        }
        employee.IsActive = request.IsActive;
        employee.ParentId = request.ParentId ?? employee.ParentId;
        employee.HireDate = request.HireDate ?? employee.HireDate;
        employee.UpdatedAt = DateTime.UtcNow;

        DirectoryHrProfileHelper.ApplyManagers(
            employee, request.ChefDeProjetId, request.SuperviseurId, request.ReferentTechniqueId);

        if (employee.ParentId is null)
            employee.ParentId = await hierarchy.ResolveDefaultParentIdAsync(employee, ct);

        await DirectoryHrProfileHelper.UpsertAsync(
            db, outbox, employee.Id, request.HrProfile, employee.HireDate, ct);
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

    public async Task<StructuralRoleAssignmentResult> AssignStructureRoleAsync(string kind, string nodeId, Guid employeeId, Guid? changedBy, string? reason, CancellationToken ct = default)
    {
        if (!Enum.TryParse<DomainAssignmentKind>(kind, true, out var assignmentKind))
            throw new ArgumentException($"Kind invalide : {kind}");

        var trimmedNodeId = nodeId.Trim();
        var nodeLevel = assignmentKind switch
        {
            DomainAssignmentKind.ChefDeProjet => DomainNodeLevel.Pole,
            DomainAssignmentKind.Superviseur => DomainNodeLevel.Cellule,
            DomainAssignmentKind.ReferentTechnique => DomainNodeLevel.Service,
            DomainAssignmentKind.Pilote => DomainNodeLevel.Service,
            _ => DomainNodeLevel.Service,
        };

        var revoked = (await exclusivity.RevokeAllStructuralRolesForEmployeeAsync(
            employeeId, changedBy, reason ?? "Nouvelle affectation structurelle", ct)).ToList();

        var alreadyOnNode = await db.OrgAssignments.AnyAsync(
            a => a.Kind == assignmentKind
                 && a.NodeId == trimmedNodeId
                 && a.EmployeeId == employeeId
                 && a.EffectiveTo == null,
            ct);
        if (alreadyOnNode)
            throw new InvalidOperationException(
                $"Cet employé occupe déjà la charge {kind} sur ce nœud.");

        var assignment = new OrgAssignment
        {
            Id = Guid.NewGuid(),
            Kind = assignmentKind,
            NodeId = trimmedNodeId,
            NodeLevel = nodeLevel,
            EmployeeId = employeeId,
            EffectiveFrom = DateTime.UtcNow,
            ChangedBy = changedBy,
            ChangeReason = reason,
        };
        db.OrgAssignments.Add(assignment);

        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new KeyNotFoundException("Employé introuvable.");
        await hierarchy.ApplyAssignmentToEmployeeAsync(employee, assignmentKind, trimmedNodeId, ct);
        employee.UpdatedAt = DateTime.UtcNow;

        await outbox.EnqueueAsync(new DirectoryAssignmentChangedMessage
        {
            Kind = MessagingEnumMapper.ToMessage(assignmentKind),
            NodeId = trimmedNodeId,
            NodeLevel = MessagingEnumMapper.ToMessage(nodeLevel),
            EmployeeId = employeeId,
            EmployeeEmail = employee.Email,
            NewRole = employee.Role,
            Removed = false,
        }, aggregateId: employeeId.ToString(), ct: ct);

        await EnqueueEmployeeChangedAsync(employee, isDeleted: false, emitLegacyCreate: false, ct);
        await db.SaveChangesAsync(ct);
        return new StructuralRoleAssignmentResult(revoked);
    }

    public async Task<bool> RemoveStructurePilotAsync(string serviceId, Guid employeeId, Guid? changedBy, string? reason, CancellationToken ct = default)
    {
        var trimmedServiceId = serviceId.Trim();
        var rows = await db.OrgAssignments
            .Where(a => a.Kind == DomainAssignmentKind.Pilote
                && a.NodeId == trimmedServiceId
                && a.EmployeeId == employeeId
                && a.EffectiveTo == null)
            .ToListAsync(ct);

        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct);

        // Pilotes projetés sans ligne OrgAssignment : l'import/bootstrap Prime estampille
        // Role=Pilote + ServiceId/CelluleId sur l'employé sans tracer d'affectation. On tolère
        // ce cas pour que « Retirer » fonctionne au lieu de renvoyer 404 (Not Found).
        var projectedPilote = employee is not null
            && string.Equals(employee.Role, KyntusRoleNames.Pilote, StringComparison.OrdinalIgnoreCase)
            && (string.Equals(employee.ServiceId, trimmedServiceId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(employee.CelluleId, trimmedServiceId, StringComparison.OrdinalIgnoreCase));

        if (rows.Count == 0 && !projectedPilote)
            return false;

        var now = DateTime.UtcNow;
        foreach (var row in rows)
        {
            row.EffectiveTo = now;
            db.OrgAssignmentHistories.Add(new OrgAssignmentHistory
            {
                Id = Guid.NewGuid(),
                Kind = DomainAssignmentKind.Pilote,
                NodeId = trimmedServiceId,
                NodeLevel = DomainNodeLevel.Service,
                PreviousEmployeeId = employeeId,
                NewEmployeeId = null,
                ChangedBy = changedBy,
                ChangeReason = reason,
                ChangedAt = now,
            });
        }

        if (employee is not null)
        {
            employee.Role = KyntusRoleNames.Employee;
            employee.ServiceId = null;
            employee.CelluleId = null;
            employee.PoleId = null;
            employee.ParentId = null;
            employee.UpdatedAt = now;
            await EnqueueEmployeeChangedAsync(employee, isDeleted: false, emitLegacyCreate: false, ct);
        }

        // Notifie le retrait une fois (ligne tracée ou pilote projeté) pour resynchroniser l'aval.
        await outbox.EnqueueAsync(new DirectoryAssignmentChangedMessage
        {
            Kind = MessagingEnumMapper.ToMessage(DomainAssignmentKind.Pilote),
            NodeId = trimmedServiceId,
            NodeLevel = MessagingEnumMapper.ToMessage(DomainNodeLevel.Service),
            EmployeeId = employeeId,
            Removed = true,
        }, aggregateId: employeeId.ToString(), ct: ct);

        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task ResetDisplacedEmployeeAsync(Guid employeeId, Guid? changedBy, string? reason, CancellationToken ct)
    {
        var hasOtherActive = await db.OrgAssignments
            .AnyAsync(a => a.EmployeeId == employeeId && a.EffectiveTo == null, ct);
        if (hasOtherActive) return;

        var isManager = await db.BusinessDepartments
            .AnyAsync(d => d.ManagerEmployeeId == employeeId, ct);
        if (isManager) return;

        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null) return;

        employee.Role = KyntusRoleNames.Employee;
        employee.PoleId = null;
        employee.CelluleId = null;
        employee.ServiceId = null;
        employee.BusinessDepartmentId = null;
        employee.ParentId = null;
        employee.UpdatedAt = DateTime.UtcNow;
        await EnqueueEmployeeChangedAsync(employee, isDeleted: false, emitLegacyCreate: false, ct);
    }

    public async Task ClearStructureRoleAsync(string kind, string nodeId, Guid? changedBy, string? reason, CancellationToken ct = default)
    {
        if (!Enum.TryParse<DomainAssignmentKind>(kind, true, out var assignmentKind))
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
                Kind = MessagingEnumMapper.ToMessage(assignmentKind),
                NodeId = nodeId.Trim(),
                NodeLevel = MessagingEnumMapper.ToMessage(row.NodeLevel),
                EmployeeId = row.EmployeeId,
                Removed = true,
            }, aggregateId: row.EmployeeId.ToString(), ct: ct);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<string> CreatePoleAsync(string name, Guid businessDepartmentId, CancellationToken ct = default)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName))
            throw new InvalidOperationException("Le nom du pôle est requis.");
        if (businessDepartmentId == Guid.Empty)
            throw new InvalidOperationException("businessDepartmentId requis.");

        var dept = await db.BusinessDepartments
            .Include(d => d.PoleAssignments)
            .FirstOrDefaultAsync(d => d.Id == businessDepartmentId, ct)
            ?? throw new KeyNotFoundException("Département introuvable.");
        if (dept.Kind != BusinessDepartmentKind.Operational)
            throw new InvalidOperationException("Seuls les départements opérationnels peuvent recevoir des pôles.");
        if (!dept.IsActive)
            throw new InvalidOperationException("Département inactif.");

        var id = NewOrgId("pole");
        db.OrgPoles.Add(new OrgPole
        {
            Id = id,
            Name = trimmedName,
            BusinessDepartmentId = businessDepartmentId,
        });
        await SyncJunctionForPoleAsync(dept, id, ct);
        dept.UpdatedAt = DateTime.UtcNow;

        await outbox.EnqueueAsync(new DirectoryOrgNodeChangedMessage
        {
            NodeId = id,
            Name = trimmedName,
            Level = MessagingEnumMapper.ToMessage(DomainNodeLevel.Pole),
        }, aggregateId: id, ct: ct);
        await EnqueueBusinessDepartmentChangedAsync(dept, ct);
        await db.SaveChangesAsync(ct);
        return id;
    }

    public async Task<bool> AttachPoleToBusinessDepartmentAsync(string poleId, Guid businessDepartmentId, CancellationToken ct = default)
    {
        var trimmedPoleId = poleId.Trim();
        var pole = await db.OrgPoles.FirstOrDefaultAsync(p => p.Id == trimmedPoleId, ct)
            ?? throw new KeyNotFoundException("Pôle introuvable.");

        var dept = await db.BusinessDepartments
            .Include(d => d.PoleAssignments)
            .FirstOrDefaultAsync(d => d.Id == businessDepartmentId, ct)
            ?? throw new KeyNotFoundException("Département introuvable.");
        if (dept.Kind != BusinessDepartmentKind.Operational)
            throw new InvalidOperationException("Seuls les départements opérationnels peuvent recevoir des pôles.");
        if (!dept.IsActive)
            throw new InvalidOperationException("Département inactif.");

        if (pole.BusinessDepartmentId.HasValue && pole.BusinessDepartmentId != businessDepartmentId)
        {
            var oldDept = await db.BusinessDepartments
                .Include(d => d.PoleAssignments)
                .FirstOrDefaultAsync(d => d.Id == pole.BusinessDepartmentId.Value, ct);
            if (oldDept is not null)
            {
                var oldRow = oldDept.PoleAssignments.FirstOrDefault(p => p.PoleId == trimmedPoleId);
                if (oldRow is not null)
                    db.DepartmentPoleAssignments.Remove(oldRow);
                oldDept.UpdatedAt = DateTime.UtcNow;
                await EnqueueBusinessDepartmentChangedAsync(oldDept, ct);
            }
        }

        pole.BusinessDepartmentId = businessDepartmentId;
        await SyncJunctionForPoleAsync(dept, trimmedPoleId, ct);
        dept.UpdatedAt = DateTime.UtcNow;
        await EnqueueBusinessDepartmentChangedAsync(dept, ct);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<string> CreateCelluleAsync(string poleId, string name, CancellationToken ct = default)
    {
        var id = NewOrgId("cell");
        db.OrgCellules.Add(new OrgCellule { Id = id, Name = name.Trim(), PoleId = poleId.Trim() });
        await outbox.EnqueueAsync(new DirectoryOrgNodeChangedMessage
        {
            NodeId = id,
            Name = name.Trim(),
            Level = MessagingEnumMapper.ToMessage(DomainNodeLevel.Cellule),
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
            Level = MessagingEnumMapper.ToMessage(DomainNodeLevel.Service),
            ParentNodeId = celluleId.Trim(),
        }, aggregateId: id, ct: ct);
        await db.SaveChangesAsync(ct);
        return id;
    }

    public async Task<bool> RenameOrgNodeAsync(DomainNodeLevel level, string nodeId, string name, CancellationToken ct = default)
    {
        var trimmedId = nodeId.Trim();
        var trimmedName = name.Trim();
        switch (level)
        {
            case DomainNodeLevel.Pole:
            {
                var row = await db.OrgPoles.FirstOrDefaultAsync(p => p.Id == trimmedId, ct);
                if (row is null) return false;
                row.Name = trimmedName;
                break;
            }
            case DomainNodeLevel.Cellule:
            {
                var row = await db.OrgCellules.FirstOrDefaultAsync(c => c.Id == trimmedId, ct);
                if (row is null) return false;
                row.Name = trimmedName;
                break;
            }
            case DomainNodeLevel.Service:
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
            Level = MessagingEnumMapper.ToMessage(level),
        }, aggregateId: trimmedId, ct: ct);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteOrgNodeAsync(DomainNodeLevel level, string nodeId, CancellationToken ct = default)
    {
        var trimmedId = nodeId.Trim();
        switch (level)
        {
            case DomainNodeLevel.Pole:
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
            case DomainNodeLevel.Cellule:
            {
                var cellule = await db.OrgCellules
                    .Include(c => c.Services)
                    .FirstOrDefaultAsync(c => c.Id == trimmedId, ct);
                if (cellule is null) return false;
                db.OrgServices.RemoveRange(cellule.Services);
                db.OrgCellules.Remove(cellule);
                break;
            }
            case DomainNodeLevel.Service:
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
            Level = MessagingEnumMapper.ToMessage(level),
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

    public async Task<BusinessDepartmentDto> CreateBusinessDepartmentAsync(CreateBusinessDepartmentRequest request, CancellationToken ct = default)
    {
        var kind = ParseKind(request.Kind);
        var code = string.IsNullOrWhiteSpace(request.Code)
            ? await GenerateBusinessDepartmentCodeAsync(kind, ct)
            : request.Code.Trim().ToUpperInvariant();
        if (await db.BusinessDepartments.AnyAsync(d => d.Code == code, ct))
            throw new InvalidOperationException($"Code département déjà utilisé : {code}");

        var dept = new BusinessDepartment
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = request.Name.Trim(),
            Kind = kind,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.BusinessDepartments.Add(dept);
        await EnqueueBusinessDepartmentChangedAsync(dept, ct);
        await db.SaveChangesAsync(ct);
        return await MapBusinessDepartmentAsync(dept, ct);
    }

    public async Task<BusinessDepartmentDto?> UpdateBusinessDepartmentAsync(Guid id, UpdateBusinessDepartmentRequest request, CancellationToken ct = default)
    {
        var dept = await db.BusinessDepartments.Include(d => d.PoleAssignments).FirstOrDefaultAsync(d => d.Id == id, ct);
        if (dept is null) return null;
        dept.Name = request.Name.Trim();
        dept.Kind = ParseKind(request.Kind);
        dept.IsActive = request.IsActive;
        dept.UpdatedAt = DateTime.UtcNow;
        if (dept.Kind == BusinessDepartmentKind.Support)
        {
            db.DepartmentPoleAssignments.RemoveRange(dept.PoleAssignments);
            dept.PoleAssignments.Clear();
        }
        await EnqueueBusinessDepartmentChangedAsync(dept, ct);
        await db.SaveChangesAsync(ct);
        return await MapBusinessDepartmentAsync(dept, ct);
    }

    public async Task<bool> DeleteBusinessDepartmentAsync(Guid id, CancellationToken ct = default)
    {
        var dept = await db.BusinessDepartments.Include(d => d.PoleAssignments).FirstOrDefaultAsync(d => d.Id == id, ct);
        if (dept is null) return false;
        dept.IsActive = false;
        dept.UpdatedAt = DateTime.UtcNow;
        await EnqueueBusinessDepartmentChangedAsync(dept, ct, isDeleted: true);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> AssignPoleToBusinessDepartmentAsync(Guid departmentId, string poleId, CancellationToken ct = default)
    {
        return await AttachPoleToBusinessDepartmentAsync(poleId, departmentId, ct);
    }

    public async Task<bool> RemovePoleFromBusinessDepartmentAsync(Guid departmentId, string poleId, CancellationToken ct = default)
    {
        var dept = await db.BusinessDepartments.Include(d => d.PoleAssignments).FirstOrDefaultAsync(d => d.Id == departmentId, ct);
        if (dept is null) return false;
        var trimmedPoleId = poleId.Trim();
        var row = dept.PoleAssignments.FirstOrDefault(p => p.PoleId == trimmedPoleId);
        var pole = await db.OrgPoles.FirstOrDefaultAsync(p => p.Id == trimmedPoleId, ct);
        if (row is null && pole?.BusinessDepartmentId != departmentId) return false;

        if (row is not null)
            db.DepartmentPoleAssignments.Remove(row);
        if (pole is not null && pole.BusinessDepartmentId == departmentId)
            pole.BusinessDepartmentId = null;

        dept.UpdatedAt = DateTime.UtcNow;
        await EnqueueBusinessDepartmentChangedAsync(dept, ct);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<StructuralRoleAssignmentResult> SetBusinessDepartmentManagerAsync(
        Guid departmentId,
        Guid employeeId,
        Guid? changedBy = null,
        string? reason = null,
        CancellationToken ct = default)
    {
        var dept = await db.BusinessDepartments.Include(d => d.PoleAssignments).FirstOrDefaultAsync(d => d.Id == departmentId, ct)
            ?? throw new KeyNotFoundException("Département introuvable.");
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId && e.IsActive, ct)
            ?? throw new KeyNotFoundException("Employé introuvable.");

        var revoked = (await exclusivity.RevokeAllStructuralRolesForEmployeeAsync(
            employeeId, changedBy, reason ?? "Nomination manager département", ct)).ToList();

        if (dept.ManagerEmployeeId.HasValue && dept.ManagerEmployeeId != employeeId)
            await exclusivity.DemotePreviousDepartmentManagerAsync(departmentId, employeeId, changedBy, reason, ct);

        dept.ManagerEmployeeId = employeeId;
        dept.UpdatedAt = DateTime.UtcNow;

        employee.Role = KyntusRoleNames.Manager;
        employee.BusinessDepartmentId = dept.Id;
        employee.ParentId = null;
        // Le manager est rattaché uniquement au département : aucune affectation
        // à un pôle/cellule/service précis (ni Operational ni Support).
        employee.PoleId = null;
        employee.CelluleId = null;
        employee.ServiceId = null;
        employee.UpdatedAt = DateTime.UtcNow;

        if (dept.Kind == BusinessDepartmentKind.Support)
        {
            var teamMembers = await db.Employees
                .Where(e => e.BusinessDepartmentId == dept.Id && e.Id != employeeId && e.IsActive)
                .ToListAsync(ct);
            foreach (var member in teamMembers)
            {
                member.ParentId = employeeId;
                member.UpdatedAt = DateTime.UtcNow;
                await EnqueueEmployeeChangedAsync(member, isDeleted: false, emitLegacyCreate: false, ct);
            }
        }

        await EnqueueBusinessDepartmentChangedAsync(dept, ct);
        await EnqueueEmployeeChangedAsync(employee, isDeleted: false, emitLegacyCreate: false, ct);
        await db.SaveChangesAsync(ct);
        return new StructuralRoleAssignmentResult(revoked);
    }

    public async Task<bool> ClearBusinessDepartmentManagerAsync(Guid departmentId, CancellationToken ct = default)
    {
        var dept = await db.BusinessDepartments.Include(d => d.PoleAssignments).FirstOrDefaultAsync(d => d.Id == departmentId, ct);
        if (dept is null) return false;
        dept.ManagerEmployeeId = null;
        dept.UpdatedAt = DateTime.UtcNow;
        await EnqueueBusinessDepartmentChangedAsync(dept, ct);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task EnqueueBusinessDepartmentChangedAsync(BusinessDepartment dept, CancellationToken ct, bool isDeleted = false)
    {
        var poleIds = await db.OrgPoles.AsNoTracking()
            .Where(p => p.BusinessDepartmentId == dept.Id)
            .Select(p => p.Id)
            .ToListAsync(ct);
        if (poleIds.Count == 0)
            poleIds = dept.PoleAssignments.Select(p => p.PoleId).ToList();

        await outbox.EnqueueAsync(new DirectoryBusinessDepartmentChangedMessage
        {
            BusinessDepartmentId = dept.Id,
            Code = dept.Code,
            Name = dept.Name,
            Kind = dept.Kind.ToString(),
            ManagerEmployeeId = dept.ManagerEmployeeId,
            PoleIds = poleIds,
            IsDeleted = isDeleted,
        }, aggregateId: dept.Id.ToString(), ct: ct);
    }

    private Task SyncJunctionForPoleAsync(BusinessDepartment dept, string poleId, CancellationToken ct)
    {
        if (dept.PoleAssignments.Any(p => p.PoleId == poleId)) return Task.CompletedTask;
        var row = new DepartmentPoleAssignment
        {
            BusinessDepartmentId = dept.Id,
            PoleId = poleId,
            CreatedAt = DateTime.UtcNow,
        };
        db.DepartmentPoleAssignments.Add(row);
        dept.PoleAssignments.Add(row);
        return Task.CompletedTask;
    }

    private async Task<BusinessDepartment?> ResolveBusinessDepartmentAsync(Guid? id, CancellationToken ct)
    {
        if (!id.HasValue) return null;
        return await db.BusinessDepartments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id.Value, ct);
    }

    private static BusinessDepartmentKind ParseKind(string kind) =>
        Enum.TryParse<BusinessDepartmentKind>(kind, true, out var k) ? k : BusinessDepartmentKind.Operational;

    private async Task<string> GenerateBusinessDepartmentCodeAsync(BusinessDepartmentKind kind, CancellationToken ct)
    {
        var prefix = kind == BusinessDepartmentKind.Support ? "SUP" : "OP";
        var existingCodes = await db.BusinessDepartments.AsNoTracking()
            .Where(d => d.Kind == kind && d.Code.StartsWith(prefix))
            .Select(d => d.Code)
            .ToListAsync(ct);

        var maxNum = 0;
        foreach (var existing in existingCodes)
        {
            var suffix = existing.Length > prefix.Length + 1 && existing[prefix.Length] == '-'
                ? existing[(prefix.Length + 1)..]
                : existing[prefix.Length..];
            if (int.TryParse(suffix, out var n) && n > maxNum)
                maxNum = n;
        }

        var next = maxNum + 1;
        string code;
        do
        {
            code = $"{prefix}-{next:D3}";
            next++;
        } while (await db.BusinessDepartments.AnyAsync(d => d.Code == code, ct));

        return code;
    }

    private async Task<BusinessDepartmentDto> MapBusinessDepartmentAsync(BusinessDepartment d, CancellationToken ct)
    {
        var poleIds = await db.OrgPoles.AsNoTracking()
            .Where(p => p.BusinessDepartmentId == d.Id)
            .Select(p => p.Id)
            .ToListAsync(ct);
        if (poleIds.Count == 0)
            poleIds = d.PoleAssignments.Select(p => p.PoleId).ToList();
        return MapBusinessDepartment(d, poleIds);
    }

    private static BusinessDepartmentDto MapBusinessDepartment(BusinessDepartment d, IReadOnlyList<string> poleIds) => new(
        d.Id.ToString(),
        d.Code,
        d.Name,
        d.Kind.ToString(),
        d.ManagerEmployeeId?.ToString(),
        d.IsActive,
        poleIds);

    private async Task EnqueueEmployeeChangedAsync(Employee employee, bool isDeleted, bool emitLegacyCreate, CancellationToken ct)
    {
        var deptKind = employee.BusinessDepartmentId.HasValue
            ? (await db.BusinessDepartments.AsNoTracking()
                .Where(d => d.Id == employee.BusinessDepartmentId.Value)
                .Select(d => d.Kind)
                .FirstOrDefaultAsync(ct)).ToString()
            : null;

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
            BusinessDepartmentId = employee.BusinessDepartmentId,
            BusinessDepartmentKind = deptKind,
            IsActive = employee.IsActive,
            IsDeleted = isDeleted,
            HireDate = employee.HireDate,
            ChefDeProjetId = employee.ChefDeProjetId,
            SuperviseurId = employee.SuperviseurId,
            ReferentTechniqueId = employee.ReferentTechniqueId,
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
        null,
        e.BusinessDepartmentId?.ToString(),
        null,
        e.ChefDeProjetId?.ToString(),
        e.SuperviseurId?.ToString(),
        e.ReferentTechniqueId?.ToString());
}
