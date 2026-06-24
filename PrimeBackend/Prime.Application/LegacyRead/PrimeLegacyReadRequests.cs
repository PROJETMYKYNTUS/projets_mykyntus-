using MediatR;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Domain.Entities;

namespace Prime.Application.LegacyRead;

public record GetAdminDashboardQuery : IRequest<AdminDashboardResponse>;

public sealed class GetAdminDashboardQueryHandler(IPrimeAdminReadAppService admin)
    : IRequestHandler<GetAdminDashboardQuery, AdminDashboardResponse>
{
    public Task<AdminDashboardResponse> Handle(GetAdminDashboardQuery request, CancellationToken ct) =>
        Task.FromResult(admin.GetAdminDashboard());
}

public record GetAdminCalculationConfigQuery : IRequest<AdminCalculationConfig>;

public sealed class GetAdminCalculationConfigQueryHandler(IPrimeAdminReadAppService admin)
    : IRequestHandler<GetAdminCalculationConfigQuery, AdminCalculationConfig>
{
    public Task<AdminCalculationConfig> Handle(GetAdminCalculationConfigQuery request, CancellationToken ct) =>
        Task.FromResult(admin.GetCalculationConfig());
}

public record SaveAdminCalculationConfigCommand(AdminCalculationConfig Payload) : IRequest<AdminCalculationConfig>;

public sealed class SaveAdminCalculationConfigCommandHandler(IPrimeAdminReadAppService admin)
    : IRequestHandler<SaveAdminCalculationConfigCommand, AdminCalculationConfig>
{
    public Task<AdminCalculationConfig> Handle(SaveAdminCalculationConfigCommand request, CancellationToken ct) =>
        Task.FromResult(admin.SaveCalculationConfig(request.Payload));
}

public record GetAdminRbacMatrixQuery : IRequest<List<AdminRbacRow>>;

public sealed class GetAdminRbacMatrixQueryHandler(IPrimeAdminReadAppService admin)
    : IRequestHandler<GetAdminRbacMatrixQuery, List<AdminRbacRow>>
{
    public Task<List<AdminRbacRow>> Handle(GetAdminRbacMatrixQuery request, CancellationToken ct) =>
        Task.FromResult(admin.GetRbacMatrix());
}

public record ToggleAdminRbacPermissionCommand(ToggleRbacPermissionRequest Body) : IRequest<List<AdminRbacRow>>;

public sealed class ToggleAdminRbacPermissionCommandHandler(IPrimeAdminReadAppService admin)
    : IRequestHandler<ToggleAdminRbacPermissionCommand, List<AdminRbacRow>>
{
    public Task<List<AdminRbacRow>> Handle(ToggleAdminRbacPermissionCommand request, CancellationToken ct) =>
        Task.FromResult(admin.ToggleRbacPermission(request.Body.Role, request.Body.Permission));
}

public record GetAdminWorkflowConfigQuery : IRequest<AdminWorkflowConfig>;

public sealed class GetAdminWorkflowConfigQueryHandler(IPrimeAdminReadAppService admin)
    : IRequestHandler<GetAdminWorkflowConfigQuery, AdminWorkflowConfig>
{
    public Task<AdminWorkflowConfig> Handle(GetAdminWorkflowConfigQuery request, CancellationToken ct) =>
        Task.FromResult(admin.GetWorkflowConfig());
}

public record SaveAdminWorkflowConfigCommand(AdminWorkflowConfig Payload) : IRequest<AdminWorkflowConfig>;

public sealed class SaveAdminWorkflowConfigCommandHandler(IPrimeAdminReadAppService admin)
    : IRequestHandler<SaveAdminWorkflowConfigCommand, AdminWorkflowConfig>
{
    public Task<AdminWorkflowConfig> Handle(SaveAdminWorkflowConfigCommand request, CancellationToken ct) =>
        Task.FromResult(admin.SaveWorkflowConfig(request.Payload));
}

public record GetAdminAuditLogsQuery : IRequest<List<AdminAuditLog>>;

public sealed class GetAdminAuditLogsQueryHandler(IPrimeAdminReadAppService admin)
    : IRequestHandler<GetAdminAuditLogsQuery, List<AdminAuditLog>>
{
    public Task<List<AdminAuditLog>> Handle(GetAdminAuditLogsQuery request, CancellationToken ct) =>
        admin.GetAuditLogsAsync(ct);
}

public record GetAdminAnomaliesQuery : IRequest<List<AdminAnomaly>>;

public sealed class GetAdminAnomaliesQueryHandler(IPrimeAdminReadAppService admin)
    : IRequestHandler<GetAdminAnomaliesQuery, List<AdminAnomaly>>
{
    public Task<List<AdminAnomaly>> Handle(GetAdminAnomaliesQuery request, CancellationToken ct) =>
        Task.FromResult(admin.GetAdminAnomalies());
}

public record UpdateAdminAnomalyStatusCommand(string Id, UpdateAnomalyStatusRequest Body) : IRequest<List<AdminAnomaly>>;

public sealed class UpdateAdminAnomalyStatusCommandHandler(IPrimeAdminReadAppService admin)
    : IRequestHandler<UpdateAdminAnomalyStatusCommand, List<AdminAnomaly>>
{
    public Task<List<AdminAnomaly>> Handle(UpdateAdminAnomalyStatusCommand request, CancellationToken ct) =>
        Task.FromResult(admin.UpdateAnomalyStatus(request.Id, request.Body.Status));
}

public record GetAuditDashboardQuery : IRequest<AuditDashboardResponse>;

public sealed class GetAuditDashboardQueryHandler(IPrimeAdminReadAppService admin)
    : IRequestHandler<GetAuditDashboardQuery, AuditDashboardResponse>
{
    public Task<AuditDashboardResponse> Handle(GetAuditDashboardQuery request, CancellationToken ct) =>
        Task.FromResult(admin.GetAuditDashboard());
}

public record GetAuditOperationsQuery : IRequest<List<AuditOperation>>;

public sealed class GetAuditOperationsQueryHandler(IPrimeAdminReadAppService admin)
    : IRequestHandler<GetAuditOperationsQuery, List<AuditOperation>>
{
    public Task<List<AuditOperation>> Handle(GetAuditOperationsQuery request, CancellationToken ct) =>
        Task.FromResult(admin.GetOperations());
}

public record GetAuditTrailLogsQuery : IRequest<List<AuditTrailLog>>;

public sealed class GetAuditTrailLogsQueryHandler(IPrimeAdminReadAppService admin)
    : IRequestHandler<GetAuditTrailLogsQuery, List<AuditTrailLog>>
{
    public Task<List<AuditTrailLog>> Handle(GetAuditTrailLogsQuery request, CancellationToken ct) =>
        Task.FromResult(admin.GetAuditTrailLogs());
}

public record GetAuditAnomaliesQuery : IRequest<List<AuditAnomaly>>;

public sealed class GetAuditAnomaliesQueryHandler(IPrimeAdminReadAppService admin)
    : IRequestHandler<GetAuditAnomaliesQuery, List<AuditAnomaly>>
{
    public Task<List<AuditAnomaly>> Handle(GetAuditAnomaliesQuery request, CancellationToken ct) =>
        Task.FromResult(admin.GetAuditAnomalies());
}

public record GetSupervisorPrimesQuery(string SupervisorUserId, string? Period) : IRequest<List<SupervisorPrimeRow>>;

public sealed class GetSupervisorPrimesQueryHandler(IPrimeAdminReadAppService admin)
    : IRequestHandler<GetSupervisorPrimesQuery, List<SupervisorPrimeRow>>
{
    public Task<List<SupervisorPrimeRow>> Handle(GetSupervisorPrimesQuery request, CancellationToken ct) =>
        Task.FromResult(admin.GetSupervisorPrimes(request.SupervisorUserId, request.Period));
}

public record SupervisorValidateCommand(SupervisorValidateRequest Body) : IRequest<SupervisorPrimeRow>;

public sealed class SupervisorValidateCommandHandler(IPrimeAdminReadAppService admin)
    : IRequestHandler<SupervisorValidateCommand, SupervisorPrimeRow>
{
    public Task<SupervisorPrimeRow> Handle(SupervisorValidateCommand request, CancellationToken ct) =>
        Task.FromResult(admin.ValidateAsSupervisor(request.Body.SupervisorUserId, request.Body.ResultId));
}

public record SupervisorRejectCommand(SupervisorRejectRequest Body) : IRequest<SupervisorPrimeRow>;

public sealed class SupervisorRejectCommandHandler(IPrimeAdminReadAppService admin)
    : IRequestHandler<SupervisorRejectCommand, SupervisorPrimeRow>
{
    public Task<SupervisorPrimeRow> Handle(SupervisorRejectCommand request, CancellationToken ct) =>
        Task.FromResult(admin.RejectAsSupervisor(request.Body.SupervisorUserId, request.Body.ResultId));
}

public record SupervisorCalculateCommand(SupervisorCalculateRequest Body) : IRequest<SupervisorCalculateResponse>;

public sealed class SupervisorCalculateCommandHandler(IPrimeAdminReadAppService admin)
    : IRequestHandler<SupervisorCalculateCommand, SupervisorCalculateResponse>
{
    public Task<SupervisorCalculateResponse> Handle(SupervisorCalculateCommand request, CancellationToken ct) =>
        Task.FromResult(admin.ComputePrimeSupervisor(request.Body));
}

public record GetSupervisorDashboardQuery(string SupervisorUserId) : IRequest<SupervisorDashboardResponse>;

public sealed class GetSupervisorDashboardQueryHandler(IPrimeAdminReadAppService admin)
    : IRequestHandler<GetSupervisorDashboardQuery, SupervisorDashboardResponse>
{
    public Task<SupervisorDashboardResponse> Handle(GetSupervisorDashboardQuery request, CancellationToken ct) =>
        Task.FromResult(admin.GetSupervisorDashboard(request.SupervisorUserId));
}

public record GetPrimeConfigsQuery(string? Kind, string? Sector, string? GroupCode, string? ActivityType)
    : IRequest<List<PrimeConfigItem>>;

public sealed class GetPrimeConfigsQueryHandler(IPrimeAdminReadAppService admin)
    : IRequestHandler<GetPrimeConfigsQuery, List<PrimeConfigItem>>
{
    public Task<List<PrimeConfigItem>> Handle(GetPrimeConfigsQuery request, CancellationToken ct) =>
        Task.FromResult(admin.GetPrimeConfigs(request.Kind, request.Sector, request.GroupCode, request.ActivityType));
}

public record CreatePrimeConfigCommand(PrimeConfigUpsertRequest Body) : IRequest<PrimeConfigItem>;

public sealed class CreatePrimeConfigCommandHandler(IPrimeAdminReadAppService admin)
    : IRequestHandler<CreatePrimeConfigCommand, PrimeConfigItem>
{
    public Task<PrimeConfigItem> Handle(CreatePrimeConfigCommand request, CancellationToken ct) =>
        Task.FromResult(admin.CreatePrimeConfig(request.Body));
}

public record UpdatePrimeConfigCommand(string Id, PrimeConfigUpsertRequest Body) : IRequest<PrimeConfigItem>;

public sealed class UpdatePrimeConfigCommandHandler(IPrimeAdminReadAppService admin)
    : IRequestHandler<UpdatePrimeConfigCommand, PrimeConfigItem>
{
    public Task<PrimeConfigItem> Handle(UpdatePrimeConfigCommand request, CancellationToken ct) =>
        Task.FromResult(admin.UpdatePrimeConfig(request.Id, request.Body));
}

public record DeletePrimeConfigCommand(string Id) : IRequest;

public sealed class DeletePrimeConfigCommandHandler(IPrimeAdminReadAppService admin)
    : IRequestHandler<DeletePrimeConfigCommand>
{
    public Task Handle(DeletePrimeConfigCommand request, CancellationToken ct)
    {
        admin.DeletePrimeConfig(request.Id);
        return Task.CompletedTask;
    }
}
