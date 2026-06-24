using Kyntus.Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Prime.Application;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Domain.Entities;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Services;

public sealed class PrimeOrgAssignmentsAppService(
    PrimeDbContext db,
    PrimeOrgStructureCommandService orgCommands,
    PrimeOrgScopeService org,
    IConfiguration configuration,
    IOrgStructureEventPublisher orgEvents,
    IEmployeeDirectorySyncService employeeDirectorySync) : IPrimeOrgAssignmentsAppService
{
    private static string NewPersistedOrgId(string prefix)
    {
        var s = Guid.NewGuid().ToString("N");
        return $"{prefix}-{s[..Math.Min(12, s.Length)]}";
    }

    private async Task ExecuteOrgStructureMutationAsync(
        CancellationToken ct,
        Func<Task> mutationAsync,
        Func<Task>? afterMutationAsync = null)
    {
        await mutationAsync();
        if (afterMutationAsync is not null)
            await afterMutationAsync();
        await db.SaveChangesAsync(ct);
    }

    private async Task PublishStructureAssignmentAsync(
        OrgAssignmentKind kind,
        string nodeId,
        OrgNodeLevel nodeLevel,
        string employeeId,
        bool removed,
        CancellationToken ct)
    {
        string? email = null;
        string? newRole = null;
        if (!removed && !string.IsNullOrWhiteSpace(employeeId))
        {
            var emp = await db.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == employeeId.Trim(), ct);
            email = emp?.Email;
            newRole = emp?.Role;
        }

        await orgEvents.PublishAssignmentChangedAsync(new OrgAssignmentChangedMessage
        {
            Kind = kind,
            NodeId = nodeId,
            NodeLevel = nodeLevel,
            EmployeeId = employeeId.Trim(),
            EmployeeEmail = email,
            NewRole = newRole,
            Removed = removed
        }, ct);
    }

    public async Task<EnsureEmployeeFromPlanningResultDto> EnsureEmployeeFromPlanningAsync(
        EnsureEmployeeFromPlanningRequest body,
        CancellationToken ct = default)
    {
        if (body.EmployeeId == Guid.Empty)
            throw new ArgumentException("employeeId est requis.");
        if (string.IsNullOrWhiteSpace(body.Email))
            throw new ArgumentException("email est requis.");

        var employeeId = await employeeDirectorySync.EnsureFromPlanningAsync(
            new EmployeeDirectoryUpsertRequest(
                EmployeeId: body.EmployeeId,
                FirstName: body.FirstName.Trim(),
                LastName: body.LastName.Trim(),
                Email: body.Email.Trim(),
                Role: body.Role,
                PrimeServiceId: body.PrimeServiceId),
            ct);

        return new EnsureEmployeeFromPlanningResultDto(employeeId);
    }

    public async Task<DedupeEmployeesResultDto> DedupeEmployeesByEmailAsync(CancellationToken ct = default)
    {
        var merged = await employeeDirectorySync.DedupeByEmailAsync(ct);
        return new DedupeEmployeesResultDto(merged);
    }

    public async Task<IReadOnlyList<PoleNode>> GetEtagesAsync(CancellationToken ct = default) =>
        await db.Poles.AsNoTracking()
            .OrderBy(p => p.Id)
            .Select(p => new PoleNode { Id = p.Id, Name = p.Name })
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CelluleNode>> GetServicesAsync(CancellationToken ct = default) =>
        await db.Cellules.AsNoTracking()
            .OrderBy(c => c.PoleId).ThenBy(c => c.Id)
            .Select(c => new CelluleNode { Id = c.Id, Name = c.Name, PoleId = c.PoleId })
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SupervisorOrgScopePoleDto>> GetSupervisorScopeAsync(
        string supervisorUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(supervisorUserId))
            throw new ArgumentException("supervisorUserId est requis.");
        return await org.GetSupervisorOrganizationalScopeAsync(supervisorUserId, ct);
    }

    public async Task<IReadOnlyList<CelluleNode>> GetSousServicesAsync(CancellationToken ct = default) =>
        await db.Services.AsNoTracking()
            .OrderBy(s => s.CelluleId).ThenBy(s => s.Id)
            .Select(s => new CelluleNode { Id = s.Id, Name = s.Name, ServiceId = s.CelluleId })
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ChefProjetPoleAssignment>> GetChefProjetPoleAssignmentsAsync(
        string? userId,
        CancellationToken ct = default)
    {
        var q = db.Employees.AsNoTracking().Where(e => e.Role == "Chef de projet");
        if (!string.IsNullOrWhiteSpace(userId)) q = q.Where(e => e.Id == userId.Trim());
        var rows = await q.OrderBy(e => e.Id).ToListAsync(ct);
        return rows
            .Where(e => !string.IsNullOrWhiteSpace(e.PoleId))
            .Select(e => new ChefProjetPoleAssignment
            {
                Id = $"m|{e.Id}|{e.PoleId}",
                UserId = e.Id,
                PoleId = e.PoleId,
            })
            .ToList();
    }

    public async Task<IReadOnlyList<SupervisorCelluleAssignment>> GetSupervisorCelluleAssignmentsAsync(
        string? userId,
        CancellationToken ct = default)
    {
        var q = db.Employees.AsNoTracking().Where(e => e.Role == "Superviseur");
        if (!string.IsNullOrWhiteSpace(userId)) q = q.Where(e => e.Id == userId.Trim());
        var rows = await q.OrderBy(e => e.Id).ToListAsync(ct);
        return rows
            .Where(e => !string.IsNullOrWhiteSpace(e.CelluleId))
            .Select(e => new SupervisorCelluleAssignment
            {
                Id = $"s|{e.Id}|{e.CelluleId}",
                UserId = e.Id,
                CelluleId = e.CelluleId,
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ReferentTechniqueServiceAssignment>> GetReferentTechniqueServiceAssignmentsAsync(
        string? userId,
        CancellationToken ct = default)
    {
        var q = db.Employees.AsNoTracking().Where(e => e.Role == "Référent technique");
        if (!string.IsNullOrWhiteSpace(userId)) q = q.Where(e => e.Id == userId.Trim());
        var rows = await q.OrderBy(e => e.Id).ToListAsync(ct);
        return rows
            .Where(e => !string.IsNullOrWhiteSpace(e.ServiceId))
            .Select(e => new ReferentTechniqueServiceAssignment
            {
                Id = $"c|{e.Id}|{e.ServiceId}",
                UserId = e.Id,
                ServiceId = e.ServiceId,
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ReferentTechniquePilotLink>> GetReferentTechniquePilotLinksAsync(
        string? coachUserId,
        CancellationToken ct = default)
    {
        var q = db.Employees.AsNoTracking().Where(e => e.Role == "Pilote" && e.ParentId != null);
        if (!string.IsNullOrWhiteSpace(coachUserId)) q = q.Where(e => e.ParentId == coachUserId.Trim());
        var rows = await q.OrderBy(e => e.Id).ToListAsync(ct);
        return rows
            .Select(e => new ReferentTechniquePilotLink
            {
                Id = $"p|{e.ParentId}|{e.Id}",
                ReferentTechniqueUserId = e.ParentId!,
                PilotUserId = e.Id,
            })
            .ToList();
    }

    public Task<ChefProjetPoleAssignment> AssignManagerEtageAsync(
        AssignChefProjetPoleRequest req,
        CancellationToken ct = default) =>
        orgCommands.AssignManagerEtageAsync(req.UserId, req.PoleId, ct);

    public Task<SupervisorCelluleAssignment> AssignSupervisorServiceAsync(
        AssignSupervisorCelluleRequest req,
        CancellationToken ct = default) =>
        orgCommands.AssignSupervisorServiceAsync(req.UserId, req.ServiceId, ct);

    public async Task<ReferentTechniqueServiceAssignment> AssignCoachSousServiceAsync(
        AssignReferentTechniqueServiceRequest req,
        CancellationToken ct = default)
    {
        ReferentTechniqueServiceAssignment? created = null;
        await ExecuteOrgStructureMutationAsync(ct, async () =>
            created = await orgCommands.AssignCoachSousServiceAsync(req.UserId, req.ServiceId, ct));
        return created!;
    }

    public async Task<ReferentTechniquePilotLink> AssignCoachPilotAsync(
        AssignReferentTechniquePilotRequest req,
        CancellationToken ct = default)
    {
        ReferentTechniquePilotLink? created = null;
        await ExecuteOrgStructureMutationAsync(ct, async () =>
            created = await orgCommands.AssignCoachPilotAsync(req.ReferentTechniqueUserId, req.PilotUserId, ct));
        return created!;
    }

    public Task RemoveChefProjetPoleAssignmentAsync(string assignmentId, CancellationToken ct = default) =>
        orgCommands.RemoveAssignmentByPrefixAsync(assignmentId, 'm', ct);

    public Task RemoveSupervisorCelluleAssignmentAsync(string assignmentId, CancellationToken ct = default) =>
        orgCommands.RemoveAssignmentByPrefixAsync(assignmentId, 's', ct);

    public Task RemoveReferentTechniqueServiceAssignmentAsync(string assignmentId, CancellationToken ct = default) =>
        orgCommands.RemoveAssignmentByPrefixAsync(assignmentId, 'c', ct);

    public Task RemoveReferentTechniquePilotLinkAsync(string linkId, CancellationToken ct = default) =>
        orgCommands.RemoveAssignmentByPrefixAsync(linkId, 'p', ct);

    public async Task<Department> CreateDepartmentAsync(CreateOrgPoleBody body, CancellationToken ct = default)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Name))
            throw new ArgumentException("Le nom du pôle est requis.");
        var name = body.Name.Trim();
        var poleNames = await db.Poles.AsNoTracking().Select(p => p.Name).ToListAsync(ct);
        try
        {
            OrgStructureRules.EnsureUniquePoleName(poleNames, name);
        }
        catch (InvalidOperationException e)
        {
            throw new PrimeApiException(409, e.Message);
        }

        var id = NewPersistedOrgId("d");
        while (await db.Poles.AnyAsync(p => p.Id == id, ct))
            id = NewPersistedOrgId("d");
        db.Poles.Add(new PoleEntity { Id = id, Name = name });
        await db.SaveChangesAsync(ct);
        if (configuration.GetValue("Prime:AutoCreateMinimalOrg", false))
            await org.EnsureRootPoleHasMinimalChildrenAsync(id, ct);
        await orgEvents.PublishNodeCreatedAsync(new OrgNodeCreatedMessage
        {
            NodeId = id,
            Name = name,
            Level = OrgNodeLevel.Pole,
            Code = $"POLE-{id}"
        }, ct);
        await db.SaveChangesAsync(ct);
        return new Department { Id = id, Name = name, Poles = [] };
    }

    public async Task<Pole> CreatePoleForDepartmentAsync(
        string departmentId,
        CreateOrgNodeNameBody body,
        CancellationToken ct = default)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Name))
            throw new ArgumentException("Le nom est requis.");
        var n = body.Name.Trim();
        var deptId = departmentId.Trim();
        if (!await db.Poles.AnyAsync(p => p.Id == deptId, ct))
            throw new KeyNotFoundException("Pôle racine introuvable.");
        var siblingNames = await db.Cellules.AsNoTracking()
            .Where(c => c.PoleId == deptId)
            .Select(c => c.Name)
            .ToListAsync(ct);
        try
        {
            OrgStructureRules.EnsureUniqueCelluleName(siblingNames, n);
        }
        catch (InvalidOperationException e)
        {
            throw new PrimeApiException(409, e.Message);
        }

        var id = NewPersistedOrgId("p");
        while (await db.Cellules.AnyAsync(c => c.Id == id, ct))
            id = NewPersistedOrgId("p");
        db.Cellules.Add(new CelluleEntity { Id = id, Name = n, PoleId = deptId });
        await db.SaveChangesAsync(ct);
        await orgEvents.PublishNodeCreatedAsync(new OrgNodeCreatedMessage
        {
            NodeId = id,
            Name = n,
            Level = OrgNodeLevel.Cellule,
            ParentNodeId = deptId,
            Code = $"CELL-{id}"
        }, ct);
        await db.SaveChangesAsync(ct);
        return new Pole { Id = id, Name = n, PoleId = deptId, Cellules = [] };
    }

    public async Task<Cellule> CreateCelluleForPoleAsync(
        string celluleId,
        CreateOrgNodeNameBody body,
        CancellationToken ct = default)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Name))
            throw new ArgumentException("Le nom est requis.");
        var n = body.Name.Trim();
        var parentCelluleId = celluleId.Trim();
        if (!await db.Cellules.AnyAsync(c => c.Id == parentCelluleId, ct))
            throw new KeyNotFoundException("Cellule introuvable.");
        var siblingNames = await db.Services.AsNoTracking()
            .Where(s => s.CelluleId == parentCelluleId)
            .Select(s => s.Name)
            .ToListAsync(ct);
        try
        {
            OrgStructureRules.EnsureUniqueServiceName(siblingNames, n);
        }
        catch (InvalidOperationException e)
        {
            throw new PrimeApiException(409, e.Message);
        }

        var id = NewPersistedOrgId("c");
        while (await db.Services.AnyAsync(s => s.Id == id, ct))
            id = NewPersistedOrgId("c");
        db.Services.Add(new ServiceEntity { Id = id, Name = n, CelluleId = parentCelluleId });
        await db.SaveChangesAsync(ct);
        await orgEvents.PublishNodeCreatedAsync(new OrgNodeCreatedMessage
        {
            NodeId = id,
            Name = n,
            Level = OrgNodeLevel.Service,
            ParentNodeId = parentCelluleId,
            Code = $"SVC-{id}"
        }, ct);
        await db.SaveChangesAsync(ct);
        return new Cellule
        {
            Id = id,
            Name = n,
            CelluleId = parentCelluleId,
            Services =
            [
                new Team { Id = id + "-team", Name = n, CelluleId = parentCelluleId, ServiceId = id },
            ],
        };
    }

    public async Task SetManagerForDepartmentAsync(string poleId, SetOrgResponsibleBody body, CancellationToken ct = default)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.EmployeeId))
            throw new ArgumentException("employeeId est requis.");
        await ExecuteOrgStructureMutationAsync(ct,
            () => orgCommands.AssignManagerEtageAsync(body.EmployeeId, poleId, ct),
            afterMutationAsync: () => PublishStructureAssignmentAsync(
                OrgAssignmentKind.ChefDeProjet, poleId, OrgNodeLevel.Pole, body.EmployeeId.Trim(), false, ct));
    }

    public Task ClearManagerForDepartmentAsync(string poleId, CancellationToken ct = default) =>
        ExecuteOrgStructureMutationAsync(ct,
            () => orgCommands.ClearManagerForPoleAsync(poleId, ct),
            afterMutationAsync: () => PublishStructureAssignmentAsync(
                OrgAssignmentKind.ChefDeProjet, poleId, OrgNodeLevel.Pole, string.Empty, true, ct));

    public async Task SetSupervisorForPoleAsync(string celluleId, SetOrgResponsibleBody body, CancellationToken ct = default)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.EmployeeId))
            throw new ArgumentException("employeeId est requis.");
        await ExecuteOrgStructureMutationAsync(ct,
            () => orgCommands.AssignSupervisorServiceAsync(body.EmployeeId, celluleId, ct),
            afterMutationAsync: () => PublishStructureAssignmentAsync(
                OrgAssignmentKind.Superviseur, celluleId, OrgNodeLevel.Cellule, body.EmployeeId.Trim(), false, ct));
    }

    public Task ClearSupervisorForPoleAsync(string celluleId, CancellationToken ct = default) =>
        ExecuteOrgStructureMutationAsync(ct, () => orgCommands.ClearSupervisorForCelluleAsync(celluleId, ct));

    public async Task SetCoachForCelluleAsync(string serviceId, SetOrgResponsibleBody body, CancellationToken ct = default)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.EmployeeId))
            throw new ArgumentException("employeeId est requis.");
        await ExecuteOrgStructureMutationAsync(ct,
            () => orgCommands.AssignCoachSousServiceAsync(body.EmployeeId, serviceId, ct),
            afterMutationAsync: () => PublishStructureAssignmentAsync(
                OrgAssignmentKind.ReferentTechnique, serviceId, OrgNodeLevel.Service, body.EmployeeId.Trim(), false, ct));
    }

    public Task ClearCoachForCelluleAsync(string serviceId, CancellationToken ct = default) =>
        ExecuteOrgStructureMutationAsync(ct, () => orgCommands.ClearCoachForServiceAsync(serviceId, ct));

    public async Task AddPilotToCelluleAsync(string serviceId, AddPilotToServiceBody body, CancellationToken ct = default)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.EmployeeId))
            throw new ArgumentException("employeeId est requis.");
        await ExecuteOrgStructureMutationAsync(ct,
            () => orgCommands.AddPilotToServiceAsync(body.EmployeeId, serviceId, ct));
    }

    public Task RemovePilotFromCelluleAsync(string serviceId, string employeeId, CancellationToken ct = default) =>
        ExecuteOrgStructureMutationAsync(ct,
            () => orgCommands.RemovePilotFromServiceAsync(employeeId, serviceId, ct));
}
