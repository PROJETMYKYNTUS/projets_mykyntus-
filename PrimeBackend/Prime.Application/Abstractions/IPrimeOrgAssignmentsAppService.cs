using Prime.Application.DTOs;
using Prime.Domain.Entities;

namespace Prime.Application.Abstractions;

public sealed record EnsureEmployeeFromPlanningResultDto(string EmployeeId);
public sealed record DedupeEmployeesResultDto(int Merged);

public interface IPrimeOrgAssignmentsAppService
{
    Task<EnsureEmployeeFromPlanningResultDto> EnsureEmployeeFromPlanningAsync(
        EnsureEmployeeFromPlanningRequest body,
        CancellationToken ct = default);

    Task<DedupeEmployeesResultDto> DedupeEmployeesByEmailAsync(CancellationToken ct = default);

    Task<IReadOnlyList<PoleNode>> GetEtagesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CelluleNode>> GetServicesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SupervisorOrgScopePoleDto>> GetSupervisorScopeAsync(string supervisorUserId, CancellationToken ct = default);
    Task<IReadOnlyList<CelluleNode>> GetSousServicesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ChefProjetPoleAssignment>> GetChefProjetPoleAssignmentsAsync(string? userId, CancellationToken ct = default);
    Task<IReadOnlyList<SupervisorCelluleAssignment>> GetSupervisorCelluleAssignmentsAsync(string? userId, CancellationToken ct = default);
    Task<IReadOnlyList<ReferentTechniqueServiceAssignment>> GetReferentTechniqueServiceAssignmentsAsync(string? userId, CancellationToken ct = default);
    Task<IReadOnlyList<ReferentTechniquePilotLink>> GetReferentTechniquePilotLinksAsync(string? coachUserId, CancellationToken ct = default);

    Task<ChefProjetPoleAssignment> AssignManagerEtageAsync(AssignChefProjetPoleRequest req, CancellationToken ct = default);
    Task<SupervisorCelluleAssignment> AssignSupervisorServiceAsync(AssignSupervisorCelluleRequest req, CancellationToken ct = default);
    Task<ReferentTechniqueServiceAssignment> AssignCoachSousServiceAsync(AssignReferentTechniqueServiceRequest req, CancellationToken ct = default);
    Task<ReferentTechniquePilotLink> AssignCoachPilotAsync(AssignReferentTechniquePilotRequest req, CancellationToken ct = default);

    Task RemoveChefProjetPoleAssignmentAsync(string assignmentId, CancellationToken ct = default);
    Task RemoveSupervisorCelluleAssignmentAsync(string assignmentId, CancellationToken ct = default);
    Task RemoveReferentTechniqueServiceAssignmentAsync(string assignmentId, CancellationToken ct = default);
    Task RemoveReferentTechniquePilotLinkAsync(string linkId, CancellationToken ct = default);

    Task<Department> CreateDepartmentAsync(CreateOrgPoleBody body, CancellationToken ct = default);
    Task<Pole> CreatePoleForDepartmentAsync(string departmentId, CreateOrgNodeNameBody body, CancellationToken ct = default);
    Task<Cellule> CreateCelluleForPoleAsync(string celluleId, CreateOrgNodeNameBody body, CancellationToken ct = default);

    Task SetManagerForDepartmentAsync(string poleId, SetOrgResponsibleBody body, CancellationToken ct = default);
    Task ClearManagerForDepartmentAsync(string poleId, CancellationToken ct = default);
    Task SetSupervisorForPoleAsync(string celluleId, SetOrgResponsibleBody body, CancellationToken ct = default);
    Task ClearSupervisorForPoleAsync(string celluleId, CancellationToken ct = default);
    Task SetCoachForCelluleAsync(string serviceId, SetOrgResponsibleBody body, CancellationToken ct = default);
    Task ClearCoachForCelluleAsync(string serviceId, CancellationToken ct = default);
    Task AddPilotToCelluleAsync(string serviceId, AddPilotToServiceBody body, CancellationToken ct = default);
    Task RemovePilotFromCelluleAsync(string serviceId, string employeeId, CancellationToken ct = default);
}
