using MediatR;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Domain.Entities;

namespace Prime.Application.Org;

public record EnsureEmployeeFromPlanningCommand(EnsureEmployeeFromPlanningRequest Body)
    : IRequest<EnsureEmployeeFromPlanningResultDto>;

public sealed class EnsureEmployeeFromPlanningCommandHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<EnsureEmployeeFromPlanningCommand, EnsureEmployeeFromPlanningResultDto>
{
    public Task<EnsureEmployeeFromPlanningResultDto> Handle(EnsureEmployeeFromPlanningCommand request, CancellationToken ct) =>
        org.EnsureEmployeeFromPlanningAsync(request.Body, ct);
}

public record DedupeEmployeesByEmailCommand : IRequest<DedupeEmployeesResultDto>;

public sealed class DedupeEmployeesByEmailCommandHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<DedupeEmployeesByEmailCommand, DedupeEmployeesResultDto>
{
    public Task<DedupeEmployeesResultDto> Handle(DedupeEmployeesByEmailCommand request, CancellationToken ct) =>
        org.DedupeEmployeesByEmailAsync(ct);
}

public record GetOrgEtagesQuery : IRequest<IReadOnlyList<PoleNode>>;

public sealed class GetOrgEtagesQueryHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<GetOrgEtagesQuery, IReadOnlyList<PoleNode>>
{
    public Task<IReadOnlyList<PoleNode>> Handle(GetOrgEtagesQuery request, CancellationToken ct) =>
        org.GetEtagesAsync(ct);
}

public record GetOrgServicesQuery : IRequest<IReadOnlyList<CelluleNode>>;

public sealed class GetOrgServicesQueryHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<GetOrgServicesQuery, IReadOnlyList<CelluleNode>>
{
    public Task<IReadOnlyList<CelluleNode>> Handle(GetOrgServicesQuery request, CancellationToken ct) =>
        org.GetServicesAsync(ct);
}

public record GetOrgSupervisorScopeQuery(string SupervisorUserId) : IRequest<IReadOnlyList<SupervisorOrgScopePoleDto>>;

public sealed class GetOrgSupervisorScopeQueryHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<GetOrgSupervisorScopeQuery, IReadOnlyList<SupervisorOrgScopePoleDto>>
{
    public Task<IReadOnlyList<SupervisorOrgScopePoleDto>> Handle(GetOrgSupervisorScopeQuery request, CancellationToken ct) =>
        org.GetSupervisorScopeAsync(request.SupervisorUserId, ct);
}

public record GetOrgSousServicesQuery : IRequest<IReadOnlyList<CelluleNode>>;

public sealed class GetOrgSousServicesQueryHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<GetOrgSousServicesQuery, IReadOnlyList<CelluleNode>>
{
    public Task<IReadOnlyList<CelluleNode>> Handle(GetOrgSousServicesQuery request, CancellationToken ct) =>
        org.GetSousServicesAsync(ct);
}

public record GetChefProjetPoleAssignmentsQuery(string? UserId) : IRequest<IReadOnlyList<ChefProjetPoleAssignment>>;

public sealed class GetChefProjetPoleAssignmentsQueryHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<GetChefProjetPoleAssignmentsQuery, IReadOnlyList<ChefProjetPoleAssignment>>
{
    public Task<IReadOnlyList<ChefProjetPoleAssignment>> Handle(GetChefProjetPoleAssignmentsQuery request, CancellationToken ct) =>
        org.GetChefProjetPoleAssignmentsAsync(request.UserId, ct);
}

public record GetSupervisorCelluleAssignmentsQuery(string? UserId) : IRequest<IReadOnlyList<SupervisorCelluleAssignment>>;

public sealed class GetSupervisorCelluleAssignmentsQueryHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<GetSupervisorCelluleAssignmentsQuery, IReadOnlyList<SupervisorCelluleAssignment>>
{
    public Task<IReadOnlyList<SupervisorCelluleAssignment>> Handle(GetSupervisorCelluleAssignmentsQuery request, CancellationToken ct) =>
        org.GetSupervisorCelluleAssignmentsAsync(request.UserId, ct);
}

public record GetReferentTechniqueServiceAssignmentsQuery(string? UserId)
    : IRequest<IReadOnlyList<ReferentTechniqueServiceAssignment>>;

public sealed class GetReferentTechniqueServiceAssignmentsQueryHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<GetReferentTechniqueServiceAssignmentsQuery, IReadOnlyList<ReferentTechniqueServiceAssignment>>
{
    public Task<IReadOnlyList<ReferentTechniqueServiceAssignment>> Handle(
        GetReferentTechniqueServiceAssignmentsQuery request,
        CancellationToken ct) =>
        org.GetReferentTechniqueServiceAssignmentsAsync(request.UserId, ct);
}

public record GetReferentTechniquePilotLinksQuery(string? CoachUserId) : IRequest<IReadOnlyList<ReferentTechniquePilotLink>>;

public sealed class GetReferentTechniquePilotLinksQueryHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<GetReferentTechniquePilotLinksQuery, IReadOnlyList<ReferentTechniquePilotLink>>
{
    public Task<IReadOnlyList<ReferentTechniquePilotLink>> Handle(GetReferentTechniquePilotLinksQuery request, CancellationToken ct) =>
        org.GetReferentTechniquePilotLinksAsync(request.CoachUserId, ct);
}

public record AssignManagerEtageCommand(AssignChefProjetPoleRequest Body) : IRequest<ChefProjetPoleAssignment>;

public sealed class AssignManagerEtageCommandHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<AssignManagerEtageCommand, ChefProjetPoleAssignment>
{
    public Task<ChefProjetPoleAssignment> Handle(AssignManagerEtageCommand request, CancellationToken ct) =>
        org.AssignManagerEtageAsync(request.Body, ct);
}

public record AssignSupervisorServiceCommand(AssignSupervisorCelluleRequest Body) : IRequest<SupervisorCelluleAssignment>;

public sealed class AssignSupervisorServiceCommandHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<AssignSupervisorServiceCommand, SupervisorCelluleAssignment>
{
    public Task<SupervisorCelluleAssignment> Handle(AssignSupervisorServiceCommand request, CancellationToken ct) =>
        org.AssignSupervisorServiceAsync(request.Body, ct);
}

public record AssignCoachSousServiceCommand(AssignReferentTechniqueServiceRequest Body)
    : IRequest<ReferentTechniqueServiceAssignment>;

public sealed class AssignCoachSousServiceCommandHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<AssignCoachSousServiceCommand, ReferentTechniqueServiceAssignment>
{
    public Task<ReferentTechniqueServiceAssignment> Handle(AssignCoachSousServiceCommand request, CancellationToken ct) =>
        org.AssignCoachSousServiceAsync(request.Body, ct);
}

public record AssignCoachPilotCommand(AssignReferentTechniquePilotRequest Body) : IRequest<ReferentTechniquePilotLink>;

public sealed class AssignCoachPilotCommandHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<AssignCoachPilotCommand, ReferentTechniquePilotLink>
{
    public Task<ReferentTechniquePilotLink> Handle(AssignCoachPilotCommand request, CancellationToken ct) =>
        org.AssignCoachPilotAsync(request.Body, ct);
}

public record RemoveChefProjetPoleAssignmentCommand(string AssignmentId) : IRequest;

public sealed class RemoveChefProjetPoleAssignmentCommandHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<RemoveChefProjetPoleAssignmentCommand>
{
    public Task Handle(RemoveChefProjetPoleAssignmentCommand request, CancellationToken ct) =>
        org.RemoveChefProjetPoleAssignmentAsync(request.AssignmentId, ct);
}

public record RemoveSupervisorCelluleAssignmentCommand(string AssignmentId) : IRequest;

public sealed class RemoveSupervisorCelluleAssignmentCommandHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<RemoveSupervisorCelluleAssignmentCommand>
{
    public Task Handle(RemoveSupervisorCelluleAssignmentCommand request, CancellationToken ct) =>
        org.RemoveSupervisorCelluleAssignmentAsync(request.AssignmentId, ct);
}

public record RemoveReferentTechniqueServiceAssignmentCommand(string AssignmentId) : IRequest;

public sealed class RemoveReferentTechniqueServiceAssignmentCommandHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<RemoveReferentTechniqueServiceAssignmentCommand>
{
    public Task Handle(RemoveReferentTechniqueServiceAssignmentCommand request, CancellationToken ct) =>
        org.RemoveReferentTechniqueServiceAssignmentAsync(request.AssignmentId, ct);
}

public record RemoveReferentTechniquePilotLinkCommand(string LinkId) : IRequest;

public sealed class RemoveReferentTechniquePilotLinkCommandHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<RemoveReferentTechniquePilotLinkCommand>
{
    public Task Handle(RemoveReferentTechniquePilotLinkCommand request, CancellationToken ct) =>
        org.RemoveReferentTechniquePilotLinkAsync(request.LinkId, ct);
}

public record CreateOrgDepartmentCommand(CreateOrgPoleBody Body) : IRequest<Department>;

public sealed class CreateOrgDepartmentCommandHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<CreateOrgDepartmentCommand, Department>
{
    public Task<Department> Handle(CreateOrgDepartmentCommand request, CancellationToken ct) =>
        org.CreateDepartmentAsync(request.Body, ct);
}

public record CreateOrgPoleForDepartmentCommand(string DepartmentId, CreateOrgNodeNameBody Body) : IRequest<Pole>;

public sealed class CreateOrgPoleForDepartmentCommandHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<CreateOrgPoleForDepartmentCommand, Pole>
{
    public Task<Pole> Handle(CreateOrgPoleForDepartmentCommand request, CancellationToken ct) =>
        org.CreatePoleForDepartmentAsync(request.DepartmentId, request.Body, ct);
}

public record CreateOrgCelluleForPoleCommand(string CelluleId, CreateOrgNodeNameBody Body) : IRequest<Cellule>;

public sealed class CreateOrgCelluleForPoleCommandHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<CreateOrgCelluleForPoleCommand, Cellule>
{
    public Task<Cellule> Handle(CreateOrgCelluleForPoleCommand request, CancellationToken ct) =>
        org.CreateCelluleForPoleAsync(request.CelluleId, request.Body, ct);
}

public record SetManagerForDepartmentCommand(string PoleId, SetOrgResponsibleBody Body) : IRequest;

public sealed class SetManagerForDepartmentCommandHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<SetManagerForDepartmentCommand>
{
    public Task Handle(SetManagerForDepartmentCommand request, CancellationToken ct) =>
        org.SetManagerForDepartmentAsync(request.PoleId, request.Body, ct);
}

public record ClearManagerForDepartmentCommand(string PoleId) : IRequest;

public sealed class ClearManagerForDepartmentCommandHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<ClearManagerForDepartmentCommand>
{
    public Task Handle(ClearManagerForDepartmentCommand request, CancellationToken ct) =>
        org.ClearManagerForDepartmentAsync(request.PoleId, ct);
}

public record SetSupervisorForPoleCommand(string CelluleId, SetOrgResponsibleBody Body) : IRequest;

public sealed class SetSupervisorForPoleCommandHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<SetSupervisorForPoleCommand>
{
    public Task Handle(SetSupervisorForPoleCommand request, CancellationToken ct) =>
        org.SetSupervisorForPoleAsync(request.CelluleId, request.Body, ct);
}

public record ClearSupervisorForPoleCommand(string CelluleId) : IRequest;

public sealed class ClearSupervisorForPoleCommandHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<ClearSupervisorForPoleCommand>
{
    public Task Handle(ClearSupervisorForPoleCommand request, CancellationToken ct) =>
        org.ClearSupervisorForPoleAsync(request.CelluleId, ct);
}

public record SetCoachForCelluleCommand(string ServiceId, SetOrgResponsibleBody Body) : IRequest;

public sealed class SetCoachForCelluleCommandHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<SetCoachForCelluleCommand>
{
    public Task Handle(SetCoachForCelluleCommand request, CancellationToken ct) =>
        org.SetCoachForCelluleAsync(request.ServiceId, request.Body, ct);
}

public record ClearCoachForCelluleCommand(string ServiceId) : IRequest;

public sealed class ClearCoachForCelluleCommandHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<ClearCoachForCelluleCommand>
{
    public Task Handle(ClearCoachForCelluleCommand request, CancellationToken ct) =>
        org.ClearCoachForCelluleAsync(request.ServiceId, ct);
}

public record AddPilotToCelluleCommand(string ServiceId, AddPilotToServiceBody Body) : IRequest;

public sealed class AddPilotToCelluleCommandHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<AddPilotToCelluleCommand>
{
    public Task Handle(AddPilotToCelluleCommand request, CancellationToken ct) =>
        org.AddPilotToCelluleAsync(request.ServiceId, request.Body, ct);
}

public record RemovePilotFromCelluleCommand(string ServiceId, string EmployeeId) : IRequest;

public sealed class RemovePilotFromCelluleCommandHandler(IPrimeOrgAssignmentsAppService org)
    : IRequestHandler<RemovePilotFromCelluleCommand>
{
    public Task Handle(RemovePilotFromCelluleCommand request, CancellationToken ct) =>
        org.RemovePilotFromCelluleAsync(request.ServiceId, request.EmployeeId, ct);
}
