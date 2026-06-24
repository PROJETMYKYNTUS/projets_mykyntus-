using MediatR;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;

namespace Prime.Application.Admin;

// ---- Workflow config ----
public record ListWorkflowStepsQuery : IRequest<IReadOnlyList<WorkflowStepConfigDto>>;
public sealed class ListWorkflowStepsQueryHandler(IWorkflowConfigAdminService workflow)
    : IRequestHandler<ListWorkflowStepsQuery, IReadOnlyList<WorkflowStepConfigDto>>
{
    public Task<IReadOnlyList<WorkflowStepConfigDto>> Handle(ListWorkflowStepsQuery request, CancellationToken ct) =>
        workflow.ListStepsAsync(ct);
}

public record CreateWorkflowStepCommand(UpsertWorkflowStepConfigRequest Body) : IRequest<WorkflowStepConfigDto>;
public sealed class CreateWorkflowStepCommandHandler(IWorkflowConfigAdminService workflow)
    : IRequestHandler<CreateWorkflowStepCommand, WorkflowStepConfigDto>
{
    public Task<WorkflowStepConfigDto> Handle(CreateWorkflowStepCommand request, CancellationToken ct) =>
        workflow.CreateStepAsync(request.Body, ct);
}

public record UpdateWorkflowStepCommand(Guid Id, UpsertWorkflowStepConfigRequest Body) : IRequest<WorkflowStepConfigDto?>;
public sealed class UpdateWorkflowStepCommandHandler(IWorkflowConfigAdminService workflow)
    : IRequestHandler<UpdateWorkflowStepCommand, WorkflowStepConfigDto?>
{
    public Task<WorkflowStepConfigDto?> Handle(UpdateWorkflowStepCommand request, CancellationToken ct) =>
        workflow.UpdateStepAsync(request.Id, request.Body, ct);
}

public record RechainWorkflowStepsCommand : IRequest<IReadOnlyList<WorkflowStepConfigDto>>;
public sealed class RechainWorkflowStepsCommandHandler(IWorkflowConfigAdminService workflow)
    : IRequestHandler<RechainWorkflowStepsCommand, IReadOnlyList<WorkflowStepConfigDto>>
{
    public Task<IReadOnlyList<WorkflowStepConfigDto>> Handle(RechainWorkflowStepsCommand request, CancellationToken ct) =>
        workflow.RechainAllStepsAsync(ct);
}

public record DeleteWorkflowStepCommand(Guid Id) : IRequest<bool>;
public sealed class DeleteWorkflowStepCommandHandler(IWorkflowConfigAdminService workflow)
    : IRequestHandler<DeleteWorkflowStepCommand, bool>
{
    public Task<bool> Handle(DeleteWorkflowStepCommand request, CancellationToken ct) =>
        workflow.DeleteStepAsync(request.Id, ct);
}

public record GetWorkflowGlobalConfigQuery : IRequest<WorkflowGlobalConfigDto>;
public sealed class GetWorkflowGlobalConfigQueryHandler(IWorkflowConfigAdminService workflow)
    : IRequestHandler<GetWorkflowGlobalConfigQuery, WorkflowGlobalConfigDto>
{
    public Task<WorkflowGlobalConfigDto> Handle(GetWorkflowGlobalConfigQuery request, CancellationToken ct) =>
        workflow.GetGlobalAsync(ct);
}

public record UpdateWorkflowGlobalConfigCommand(UpdateWorkflowGlobalConfigRequest Body) : IRequest<WorkflowGlobalConfigDto>;
public sealed class UpdateWorkflowGlobalConfigCommandHandler(IWorkflowConfigAdminService workflow)
    : IRequestHandler<UpdateWorkflowGlobalConfigCommand, WorkflowGlobalConfigDto>
{
    public Task<WorkflowGlobalConfigDto> Handle(UpdateWorkflowGlobalConfigCommand request, CancellationToken ct) =>
        workflow.UpdateGlobalAsync(request.Body, ct);
}

// ---- Audit logs ----
public record ListAuditLogsQuery(AuditLogListFilter Filter) : IRequest<IReadOnlyList<AuditLogDto>>;
public sealed class ListAuditLogsQueryHandler(IAuditLogAdminService audit)
    : IRequestHandler<ListAuditLogsQuery, IReadOnlyList<AuditLogDto>>
{
    public Task<IReadOnlyList<AuditLogDto>> Handle(ListAuditLogsQuery request, CancellationToken ct) =>
        audit.ListAsync(request.Filter, ct);
}

public record RecordAuditNavigationCommand(RecordAuditNavigationRequest Body) : IRequest<Unit>;
public sealed class RecordAuditNavigationCommandHandler(IAuditLogAdminService audit)
    : IRequestHandler<RecordAuditNavigationCommand, Unit>
{
    public async Task<Unit> Handle(RecordAuditNavigationCommand request, CancellationToken ct)
    {
        await audit.RecordNavigationAsync(request.Body, ct);
        return Unit.Value;
    }
}

// ---- Anomalies ----
public record ListAnomaliesQuery(AnomalyListFilter Filter) : IRequest<IReadOnlyList<AnomalyDto>>;
public sealed class ListAnomaliesQueryHandler(IAnomalyAdminService anomalies)
    : IRequestHandler<ListAnomaliesQuery, IReadOnlyList<AnomalyDto>>
{
    public Task<IReadOnlyList<AnomalyDto>> Handle(ListAnomaliesQuery request, CancellationToken ct) =>
        anomalies.ListAsync(request.Filter, ct);
}

public record UpdateAnomalyStatusCommand(Guid Id, UpdateAnomalyStatusBody Body) : IRequest<AnomalyDto?>;
public sealed class UpdateAnomalyStatusCommandHandler(IAnomalyAdminService anomalies)
    : IRequestHandler<UpdateAnomalyStatusCommand, AnomalyDto?>
{
    public Task<AnomalyDto?> Handle(UpdateAnomalyStatusCommand request, CancellationToken ct) =>
        anomalies.UpdateStatusAsync(request.Id, request.Body, ct);
}

public record RecomputeAllAnomaliesCommand : IRequest<int>;
public sealed class RecomputeAllAnomaliesCommandHandler(IAnomalyAdminService anomalies)
    : IRequestHandler<RecomputeAllAnomaliesCommand, int>
{
    public Task<int> Handle(RecomputeAllAnomaliesCommand request, CancellationToken ct) =>
        anomalies.RecomputeAllAsync(ct);
}

// ---- Global pool workflow ----
public record ListGlobalPoolWorkflowStepsQuery : IRequest<IReadOnlyList<GlobalPoolWorkflowStepDto>>;
public sealed class ListGlobalPoolWorkflowStepsQueryHandler(IGlobalPoolWorkflowAdminService globalPool)
    : IRequestHandler<ListGlobalPoolWorkflowStepsQuery, IReadOnlyList<GlobalPoolWorkflowStepDto>>
{
    public Task<IReadOnlyList<GlobalPoolWorkflowStepDto>> Handle(ListGlobalPoolWorkflowStepsQuery request, CancellationToken ct) =>
        globalPool.ListStepsAsync(ct);
}

public record CreateGlobalPoolWorkflowStepCommand(UpsertGlobalPoolWorkflowStepRequest Body) : IRequest<GlobalPoolWorkflowStepDto>;
public sealed class CreateGlobalPoolWorkflowStepCommandHandler(IGlobalPoolWorkflowAdminService globalPool)
    : IRequestHandler<CreateGlobalPoolWorkflowStepCommand, GlobalPoolWorkflowStepDto>
{
    public Task<GlobalPoolWorkflowStepDto> Handle(CreateGlobalPoolWorkflowStepCommand request, CancellationToken ct) =>
        globalPool.CreateStepAsync(request.Body, ct);
}

public record UpdateGlobalPoolWorkflowStepCommand(Guid Id, UpsertGlobalPoolWorkflowStepRequest Body) : IRequest<GlobalPoolWorkflowStepDto?>;
public sealed class UpdateGlobalPoolWorkflowStepCommandHandler(IGlobalPoolWorkflowAdminService globalPool)
    : IRequestHandler<UpdateGlobalPoolWorkflowStepCommand, GlobalPoolWorkflowStepDto?>
{
    public Task<GlobalPoolWorkflowStepDto?> Handle(UpdateGlobalPoolWorkflowStepCommand request, CancellationToken ct) =>
        globalPool.UpdateStepAsync(request.Id, request.Body, ct);
}

public record DeleteGlobalPoolWorkflowStepCommand(Guid Id) : IRequest<bool>;
public sealed class DeleteGlobalPoolWorkflowStepCommandHandler(IGlobalPoolWorkflowAdminService globalPool)
    : IRequestHandler<DeleteGlobalPoolWorkflowStepCommand, bool>
{
    public Task<bool> Handle(DeleteGlobalPoolWorkflowStepCommand request, CancellationToken ct) =>
        globalPool.DeleteStepAsync(request.Id, ct);
}
