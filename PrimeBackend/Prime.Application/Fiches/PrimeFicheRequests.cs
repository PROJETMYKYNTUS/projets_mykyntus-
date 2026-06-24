using MediatR;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;

namespace Prime.Application.Fiches;

// ---- Employee service fiches ----
public record ListEmployeePrimeServiceFichesQuery(
    string? ServiceId,
    string? CelluleId,
    string Period,
    string SupervisorUserId) : IRequest<IReadOnlyList<EmployeePrimeServiceFicheListItemDto>>;

public sealed class ListEmployeePrimeServiceFichesQueryHandler(IEmployeePrimeServiceFicheAppService fiches)
    : IRequestHandler<ListEmployeePrimeServiceFichesQuery, IReadOnlyList<EmployeePrimeServiceFicheListItemDto>>
{
    public Task<IReadOnlyList<EmployeePrimeServiceFicheListItemDto>> Handle(
        ListEmployeePrimeServiceFichesQuery request,
        CancellationToken ct) =>
        fiches.ListAsync(request.ServiceId, request.CelluleId, request.Period, request.SupervisorUserId, ct);
}

public record GetEmployeePrimeServiceFicheForEmployeeQuery(
    string SupervisorUserId,
    string EmployeeId,
    string Period,
    string? TemplateId) : IRequest<EmployeePrimeServiceFicheResponseDto>;

public sealed class GetEmployeePrimeServiceFicheForEmployeeQueryHandler(IEmployeePrimeServiceFicheAppService fiches)
    : IRequestHandler<GetEmployeePrimeServiceFicheForEmployeeQuery, EmployeePrimeServiceFicheResponseDto>
{
    public Task<EmployeePrimeServiceFicheResponseDto> Handle(
        GetEmployeePrimeServiceFicheForEmployeeQuery request,
        CancellationToken ct) =>
        fiches.GetForEmployeeAsync(
            request.SupervisorUserId,
            request.EmployeeId,
            request.Period,
            request.TemplateId,
            ct);
}

public record UpsertEmployeePrimeServiceFicheCommand(UpsertEmployeePrimeServiceFicheRequest Body)
    : IRequest<EmployeePrimeServiceFicheResponseDto>;

public sealed class UpsertEmployeePrimeServiceFicheCommandHandler(IEmployeePrimeServiceFicheAppService fiches)
    : IRequestHandler<UpsertEmployeePrimeServiceFicheCommand, EmployeePrimeServiceFicheResponseDto>
{
    public Task<EmployeePrimeServiceFicheResponseDto> Handle(
        UpsertEmployeePrimeServiceFicheCommand request,
        CancellationToken ct) =>
        fiches.UpsertAsync(request.Body, ct);
}

public record PersistEmployeePrimeServiceFicheAmountsCommand(Guid FicheId, PersistFicheAmountsRequest Body)
    : IRequest<EmployeePrimeServiceFicheResponseDto>;

public sealed class PersistEmployeePrimeServiceFicheAmountsCommandHandler(IEmployeePrimeServiceFicheAppService fiches)
    : IRequestHandler<PersistEmployeePrimeServiceFicheAmountsCommand, EmployeePrimeServiceFicheResponseDto>
{
    public Task<EmployeePrimeServiceFicheResponseDto> Handle(
        PersistEmployeePrimeServiceFicheAmountsCommand request,
        CancellationToken ct) =>
        fiches.PersistAmountsAsync(request.FicheId, request.Body, ct);
}

// ---- Validation ----
public record ReconcileReadyValidationsCommand : IRequest<ValidationReconcileResultDto>;

public sealed class ReconcileReadyValidationsCommandHandler(IPrimeValidationAppService validation)
    : IRequestHandler<ReconcileReadyValidationsCommand, ValidationReconcileResultDto>
{
    public Task<ValidationReconcileResultDto> Handle(ReconcileReadyValidationsCommand request, CancellationToken ct) =>
        validation.ReconcileReadyAsync(ct);
}

public record GetValidationWorkflowMetaQuery(string? Role) : IRequest<WorkflowValidationMetaDto>;

public sealed class GetValidationWorkflowMetaQueryHandler(IPrimeValidationAppService validation)
    : IRequestHandler<GetValidationWorkflowMetaQuery, WorkflowValidationMetaDto>
{
    public Task<WorkflowValidationMetaDto> Handle(GetValidationWorkflowMetaQuery request, CancellationToken ct) =>
        validation.GetWorkflowMetaAsync(request.Role, ct);
}

public record ListValidationsQuery(
    string? Period,
    string? Status,
    string? ServiceId,
    string? CelluleId,
    string? UserId,
    string? Role,
    bool? ReadyOnly) : IRequest<IReadOnlyList<EmployeePrimeServiceFicheValidationDto>>;

public sealed class ListValidationsQueryHandler(IPrimeValidationAppService validation)
    : IRequestHandler<ListValidationsQuery, IReadOnlyList<EmployeePrimeServiceFicheValidationDto>>
{
    public Task<IReadOnlyList<EmployeePrimeServiceFicheValidationDto>> Handle(
        ListValidationsQuery request,
        CancellationToken ct) =>
        validation.ListAsync(
            request.Period,
            request.Status,
            request.ServiceId,
            request.CelluleId,
            request.UserId,
            request.Role,
            request.ReadyOnly,
            ct);
}

public record GetValidationSummaryQuery(
    string? Period,
    string? ServiceId,
    string? CelluleId,
    string? UserId,
    string? Role,
    bool? ReadyOnly) : IRequest<WorkflowValidationSummaryDto>;

public sealed class GetValidationSummaryQueryHandler(IPrimeValidationAppService validation)
    : IRequestHandler<GetValidationSummaryQuery, WorkflowValidationSummaryDto>
{
    public Task<WorkflowValidationSummaryDto> Handle(GetValidationSummaryQuery request, CancellationToken ct) =>
        validation.GetSummaryAsync(
            request.Period,
            request.ServiceId,
            request.CelluleId,
            request.UserId,
            request.Role,
            request.ReadyOnly,
            ct);
}

public record GetValidationHistoryFeedQuery(
    string? UserId,
    string? Role,
    string? Period,
    bool? MineOnly,
    string? Action) : IRequest<IReadOnlyList<PrimeFicheValidationHistoryFeedItemDto>>;

public sealed class GetValidationHistoryFeedQueryHandler(IPrimeValidationAppService validation)
    : IRequestHandler<GetValidationHistoryFeedQuery, IReadOnlyList<PrimeFicheValidationHistoryFeedItemDto>>
{
    public Task<IReadOnlyList<PrimeFicheValidationHistoryFeedItemDto>> Handle(
        GetValidationHistoryFeedQuery request,
        CancellationToken ct) =>
        validation.GetHistoryFeedAsync(
            request.UserId,
            request.Role,
            request.Period,
            request.MineOnly,
            request.Action,
            ct);
}

public record ListValidationPeriodsQuery : IRequest<IReadOnlyList<string>>;

public sealed class ListValidationPeriodsQueryHandler(IPrimeValidationAppService validation)
    : IRequestHandler<ListValidationPeriodsQuery, IReadOnlyList<string>>
{
    public Task<IReadOnlyList<string>> Handle(ListValidationPeriodsQuery request, CancellationToken ct) =>
        validation.ListPeriodsAsync(ct);
}

public record GetFicheValidationHistoryQuery(Guid FicheId, string? UserId, string? Role)
    : IRequest<IReadOnlyList<PrimeFicheValidationHistoryDto>>;

public sealed class GetFicheValidationHistoryQueryHandler(IPrimeValidationAppService validation)
    : IRequestHandler<GetFicheValidationHistoryQuery, IReadOnlyList<PrimeFicheValidationHistoryDto>>
{
    public Task<IReadOnlyList<PrimeFicheValidationHistoryDto>> Handle(
        GetFicheValidationHistoryQuery request,
        CancellationToken ct) =>
        validation.GetFicheHistoryAsync(request.FicheId, request.UserId, request.Role, ct);
}

public record ApproveFicheValidationCommand(Guid FicheId, ApproveServiceFicheRequest Body)
    : IRequest<EmployeePrimeServiceFicheValidationDto>;

public sealed class ApproveFicheValidationCommandHandler(IPrimeValidationAppService validation)
    : IRequestHandler<ApproveFicheValidationCommand, EmployeePrimeServiceFicheValidationDto>
{
    public Task<EmployeePrimeServiceFicheValidationDto> Handle(
        ApproveFicheValidationCommand request,
        CancellationToken ct) =>
        validation.ApproveAsync(request.FicheId, request.Body, ct);
}

public record RejectFicheValidationCommand(Guid FicheId, RejectServiceFicheRequest Body)
    : IRequest<EmployeePrimeServiceFicheValidationDto>;

public sealed class RejectFicheValidationCommandHandler(IPrimeValidationAppService validation)
    : IRequestHandler<RejectFicheValidationCommand, EmployeePrimeServiceFicheValidationDto>
{
    public Task<EmployeePrimeServiceFicheValidationDto> Handle(
        RejectFicheValidationCommand request,
        CancellationToken ct) =>
        validation.RejectAsync(request.FicheId, request.Body, ct);
}

public record BulkApproveFicheValidationsCommand(BulkApproveServiceFicheRequest Body) : IRequest<BulkApproveResultDto>;

public sealed class BulkApproveFicheValidationsCommandHandler(IPrimeValidationAppService validation)
    : IRequestHandler<BulkApproveFicheValidationsCommand, BulkApproveResultDto>
{
    public Task<BulkApproveResultDto> Handle(BulkApproveFicheValidationsCommand request, CancellationToken ct) =>
        validation.BulkApproveAsync(request.Body, ct);
}

public record ExportFicheCsvQuery(Guid FicheId, string? UserId, string? Role) : IRequest<FileExportResultDto>;

public sealed class ExportFicheCsvQueryHandler(IPrimeValidationAppService validation)
    : IRequestHandler<ExportFicheCsvQuery, FileExportResultDto>
{
    public Task<FileExportResultDto> Handle(ExportFicheCsvQuery request, CancellationToken ct) =>
        validation.ExportCsvAsync(request.FicheId, request.UserId, request.Role, ct);
}

public record ExportFicheXlsxQuery(Guid FicheId, string? UserId, string? Role) : IRequest<FileExportResultDto>;

public sealed class ExportFicheXlsxQueryHandler(IPrimeValidationAppService validation)
    : IRequestHandler<ExportFicheXlsxQuery, FileExportResultDto>
{
    public Task<FileExportResultDto> Handle(ExportFicheXlsxQuery request, CancellationToken ct) =>
        validation.ExportXlsxAsync(request.FicheId, request.UserId, request.Role, ct);
}
