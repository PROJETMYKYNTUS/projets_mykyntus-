using MediatR;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;

namespace Prime.Application.GlobalPool;

// ---- Pilotage ----
public record GetPilotageCellsSummaryQuery(string SupervisorUserId, string Period)
    : IRequest<IReadOnlyList<ServicePilotageSummaryDto>>;

public sealed class GetPilotageCellsSummaryQueryHandler(IPrimePilotageAppService pilotage)
    : IRequestHandler<GetPilotageCellsSummaryQuery, IReadOnlyList<ServicePilotageSummaryDto>>
{
    public Task<IReadOnlyList<ServicePilotageSummaryDto>> Handle(GetPilotageCellsSummaryQuery request, CancellationToken ct) =>
        pilotage.GetCellsSummaryAsync(request.SupervisorUserId, request.Period, ct);
}

// ---- Stakeholder inbox (draft-based) ----
public record GetGlobalPoolInboxQuery(string UserId, string? Role) : IRequest<IReadOnlyList<GlobalPoolInboxItemDto>>;

public sealed class GetGlobalPoolInboxQueryHandler(IPrimeGlobalPoolStakeholderAppService pool)
    : IRequestHandler<GetGlobalPoolInboxQuery, IReadOnlyList<GlobalPoolInboxItemDto>>
{
    public Task<IReadOnlyList<GlobalPoolInboxItemDto>> Handle(GetGlobalPoolInboxQuery request, CancellationToken ct) =>
        pool.GetInboxAsync(request.UserId, request.Role, ct);
}

public record DownloadGlobalPoolDraftExcelQuery(Guid DraftId, string UserId) : IRequest<FileExportResultDto>;

public sealed class DownloadGlobalPoolDraftExcelQueryHandler(IPrimeGlobalPoolStakeholderAppService pool)
    : IRequestHandler<DownloadGlobalPoolDraftExcelQuery, FileExportResultDto>
{
    public Task<FileExportResultDto> Handle(DownloadGlobalPoolDraftExcelQuery request, CancellationToken ct) =>
        pool.DownloadExcelAsync(request.DraftId, request.UserId, ct);
}

public record ApproveGlobalPoolDraftStepCommand(Guid DraftId, GlobalPoolApproveStepRequest Body)
    : IRequest<CelluleDraftGlobalPoolStateDto>;

public sealed class ApproveGlobalPoolDraftStepCommandHandler(IPrimeGlobalPoolStakeholderAppService pool)
    : IRequestHandler<ApproveGlobalPoolDraftStepCommand, CelluleDraftGlobalPoolStateDto>
{
    public Task<CelluleDraftGlobalPoolStateDto> Handle(ApproveGlobalPoolDraftStepCommand request, CancellationToken ct) =>
        pool.ApproveStepAsync(request.DraftId, request.Body, ct);
}

public record ApproveGlobalPoolDraftManagerCommand(Guid DraftId, GlobalPoolActingUserRequest Body)
    : IRequest<CelluleDraftGlobalPoolStateDto>;

public sealed class ApproveGlobalPoolDraftManagerCommandHandler(IPrimeGlobalPoolStakeholderAppService pool)
    : IRequestHandler<ApproveGlobalPoolDraftManagerCommand, CelluleDraftGlobalPoolStateDto>
{
    public Task<CelluleDraftGlobalPoolStateDto> Handle(ApproveGlobalPoolDraftManagerCommand request, CancellationToken ct) =>
        pool.ApproveManagerAsync(request.DraftId, request.Body, ct);
}

public record ApproveGlobalPoolDraftRhCommand(Guid DraftId, GlobalPoolActingUserRequest Body)
    : IRequest<CelluleDraftGlobalPoolStateDto>;

public sealed class ApproveGlobalPoolDraftRhCommandHandler(IPrimeGlobalPoolStakeholderAppService pool)
    : IRequestHandler<ApproveGlobalPoolDraftRhCommand, CelluleDraftGlobalPoolStateDto>
{
    public Task<CelluleDraftGlobalPoolStateDto> Handle(ApproveGlobalPoolDraftRhCommand request, CancellationToken ct) =>
        pool.ApproveRhAsync(request.DraftId, request.Body, ct);
}

public record AckGlobalPoolDraftComptaCommand(Guid DraftId, GlobalPoolActingUserRequest Body)
    : IRequest<CelluleDraftGlobalPoolStateDto>;

public sealed class AckGlobalPoolDraftComptaCommandHandler(IPrimeGlobalPoolStakeholderAppService pool)
    : IRequestHandler<AckGlobalPoolDraftComptaCommand, CelluleDraftGlobalPoolStateDto>
{
    public Task<CelluleDraftGlobalPoolStateDto> Handle(AckGlobalPoolDraftComptaCommand request, CancellationToken ct) =>
        pool.AckComptaAsync(request.DraftId, request.Body, ct);
}

// ---- Scope synthesis ----
public record GetGlobalPoolReadinessQuery(string Period) : IRequest<GlobalPoolReadinessDto>;

public sealed class GetGlobalPoolReadinessQueryHandler(IPrimeGlobalPoolScopeAppService scope)
    : IRequestHandler<GetGlobalPoolReadinessQuery, GlobalPoolReadinessDto>
{
    public Task<GlobalPoolReadinessDto> Handle(GetGlobalPoolReadinessQuery request, CancellationToken ct) =>
        scope.GetReadinessAsync(request.Period, ct);
}

public record GetGlobalSynthesisLinesQuery(
    string Period, string ScopeType, string ScopeId, Guid? ScopeSynthesisId, string? UserId)
    : IRequest<GlobalSynthesisLinesResponseDto>;

public sealed class GetGlobalSynthesisLinesQueryHandler(IPrimeGlobalPoolScopeAppService scope)
    : IRequestHandler<GetGlobalSynthesisLinesQuery, GlobalSynthesisLinesResponseDto>
{
    public Task<GlobalSynthesisLinesResponseDto> Handle(GetGlobalSynthesisLinesQuery request, CancellationToken ct) =>
        scope.GetSynthesisLinesAsync(
            request.Period, request.ScopeType, request.ScopeId, request.ScopeSynthesisId, request.UserId, ct);
}

public record GetGlobalSynthesisSummaryQuery(
    string Period, string ScopeType, string ScopeId, Guid? ScopeSynthesisId)
    : IRequest<GlobalSynthesisSummaryDto>;

public sealed class GetGlobalSynthesisSummaryQueryHandler(IPrimeGlobalPoolScopeAppService scope)
    : IRequestHandler<GetGlobalSynthesisSummaryQuery, GlobalSynthesisSummaryDto>
{
    public Task<GlobalSynthesisSummaryDto> Handle(GetGlobalSynthesisSummaryQuery request, CancellationToken ct) =>
        scope.GetSynthesisSummaryAsync(request.Period, request.ScopeType, request.ScopeId, request.ScopeSynthesisId, ct);
}

public record GenerateGlobalSynthesisCommand(GenerateScopeSynthesisRequest Body) : IRequest<GenerateSynthesisResultDto>;

public sealed class GenerateGlobalSynthesisCommandHandler(IPrimeGlobalPoolScopeAppService scope)
    : IRequestHandler<GenerateGlobalSynthesisCommand, GenerateSynthesisResultDto>
{
    public Task<GenerateSynthesisResultDto> Handle(GenerateGlobalSynthesisCommand request, CancellationToken ct) =>
        scope.GenerateSynthesisAsync(request.Body, ct);
}

public record EnsureGlobalSynthesisCommand(GenerateScopeSynthesisRequest Body) : IRequest<EnsureSynthesisResultDto>;

public sealed class EnsureGlobalSynthesisCommandHandler(IPrimeGlobalPoolScopeAppService scope)
    : IRequestHandler<EnsureGlobalSynthesisCommand, EnsureSynthesisResultDto>
{
    public Task<EnsureSynthesisResultDto> Handle(EnsureGlobalSynthesisCommand request, CancellationToken ct) =>
        scope.EnsureSynthesisAsync(request.Body, ct);
}

public record GetGlobalPoolScopeInboxQuery(string UserId, string? Role)
    : IRequest<IReadOnlyList<GlobalPoolScopeSynthesisInboxItemDto>>;

public sealed class GetGlobalPoolScopeInboxQueryHandler(IPrimeGlobalPoolScopeAppService scope)
    : IRequestHandler<GetGlobalPoolScopeInboxQuery, IReadOnlyList<GlobalPoolScopeSynthesisInboxItemDto>>
{
    public Task<IReadOnlyList<GlobalPoolScopeSynthesisInboxItemDto>> Handle(
        GetGlobalPoolScopeInboxQuery request, CancellationToken ct) =>
        scope.GetScopeInboxAsync(request.UserId, request.Role, ct);
}

public record DownloadGlobalPoolScopeExcelQuery(Guid ScopeSynthesisId, string UserId) : IRequest<FileExportResultDto>;

public sealed class DownloadGlobalPoolScopeExcelQueryHandler(IPrimeGlobalPoolScopeAppService scope)
    : IRequestHandler<DownloadGlobalPoolScopeExcelQuery, FileExportResultDto>
{
    public Task<FileExportResultDto> Handle(DownloadGlobalPoolScopeExcelQuery request, CancellationToken ct) =>
        scope.DownloadScopeExcelAsync(request.ScopeSynthesisId, request.UserId, ct);
}

public record ApproveGlobalPoolScopeStepCommand(Guid ScopeSynthesisId, GlobalPoolApproveStepRequest Body)
    : IRequest<GlobalPoolScopeSynthesisInboxItemDto>;

public sealed class ApproveGlobalPoolScopeStepCommandHandler(IPrimeGlobalPoolScopeAppService scope)
    : IRequestHandler<ApproveGlobalPoolScopeStepCommand, GlobalPoolScopeSynthesisInboxItemDto>
{
    public Task<GlobalPoolScopeSynthesisInboxItemDto> Handle(ApproveGlobalPoolScopeStepCommand request, CancellationToken ct) =>
        scope.ApproveScopeStepAsync(request.ScopeSynthesisId, request.Body, ct);
}

public record ApproveGlobalPoolScopeManagerCommand(Guid ScopeSynthesisId, GlobalPoolActingUserRequest Body)
    : IRequest<GlobalPoolScopeSynthesisInboxItemDto>;

public sealed class ApproveGlobalPoolScopeManagerCommandHandler(IPrimeGlobalPoolScopeAppService scope)
    : IRequestHandler<ApproveGlobalPoolScopeManagerCommand, GlobalPoolScopeSynthesisInboxItemDto>
{
    public Task<GlobalPoolScopeSynthesisInboxItemDto> Handle(ApproveGlobalPoolScopeManagerCommand request, CancellationToken ct) =>
        scope.ApproveScopeManagerAsync(request.ScopeSynthesisId, request.Body, ct);
}

public record ApproveGlobalPoolScopeRhCommand(Guid ScopeSynthesisId, GlobalPoolActingUserRequest Body)
    : IRequest<GlobalPoolScopeSynthesisInboxItemDto>;

public sealed class ApproveGlobalPoolScopeRhCommandHandler(IPrimeGlobalPoolScopeAppService scope)
    : IRequestHandler<ApproveGlobalPoolScopeRhCommand, GlobalPoolScopeSynthesisInboxItemDto>
{
    public Task<GlobalPoolScopeSynthesisInboxItemDto> Handle(ApproveGlobalPoolScopeRhCommand request, CancellationToken ct) =>
        scope.ApproveScopeRhAsync(request.ScopeSynthesisId, request.Body, ct);
}

public record AckGlobalPoolScopeComptaCommand(Guid ScopeSynthesisId, GlobalPoolActingUserRequest Body)
    : IRequest<GlobalPoolScopeSynthesisInboxItemDto>;

public sealed class AckGlobalPoolScopeComptaCommandHandler(IPrimeGlobalPoolScopeAppService scope)
    : IRequestHandler<AckGlobalPoolScopeComptaCommand, GlobalPoolScopeSynthesisInboxItemDto>
{
    public Task<GlobalPoolScopeSynthesisInboxItemDto> Handle(AckGlobalPoolScopeComptaCommand request, CancellationToken ct) =>
        scope.AckScopeComptaAsync(request.ScopeSynthesisId, request.Body, ct);
}

public record RejectGlobalSynthesisLineCommand(Guid LineId, RejectSynthesisLineRequest Body) : IRequest<Unit>;

public sealed class RejectGlobalSynthesisLineCommandHandler(IPrimeGlobalPoolScopeAppService scope)
    : IRequestHandler<RejectGlobalSynthesisLineCommand, Unit>
{
    public async Task<Unit> Handle(RejectGlobalSynthesisLineCommand request, CancellationToken ct)
    {
        await scope.RejectLineAsync(request.LineId, request.Body, ct);
        return Unit.Value;
    }
}

public record ApproveGlobalSynthesisLineCommand(Guid LineId, GlobalPoolActingUserRequest Body) : IRequest<Unit>;

public sealed class ApproveGlobalSynthesisLineCommandHandler(IPrimeGlobalPoolScopeAppService scope)
    : IRequestHandler<ApproveGlobalSynthesisLineCommand, Unit>
{
    public async Task<Unit> Handle(ApproveGlobalSynthesisLineCommand request, CancellationToken ct)
    {
        await scope.ApproveLineAsync(request.LineId, request.Body, ct);
        return Unit.Value;
    }
}

public record GetSupervisorSynthesisTrackingQuery(string SupervisorUserId, string? Period)
    : IRequest<IReadOnlyList<SupervisorSynthesisTrackingItemDto>>;

public sealed class GetSupervisorSynthesisTrackingQueryHandler(IPrimeGlobalPoolScopeAppService scope)
    : IRequestHandler<GetSupervisorSynthesisTrackingQuery, IReadOnlyList<SupervisorSynthesisTrackingItemDto>>
{
    public Task<IReadOnlyList<SupervisorSynthesisTrackingItemDto>> Handle(
        GetSupervisorSynthesisTrackingQuery request, CancellationToken ct) =>
        scope.GetSupervisorSynthesisTrackingAsync(request.SupervisorUserId, request.Period, ct);
}

public record GetMySynthesisTrackingQuery(string? UserId, string? Role)
    : IRequest<IReadOnlyList<EmployeePrimePaymentTrackingDto>>;

public sealed class GetMySynthesisTrackingQueryHandler(IPrimeGlobalPoolScopeAppService scope)
    : IRequestHandler<GetMySynthesisTrackingQuery, IReadOnlyList<EmployeePrimePaymentTrackingDto>>
{
    public Task<IReadOnlyList<EmployeePrimePaymentTrackingDto>> Handle(
        GetMySynthesisTrackingQuery request, CancellationToken ct) =>
        scope.GetMySynthesisTrackingAsync(request.UserId, request.Role, ct);
}

public record GetSynthesisTrackingFeedQuery(
    string? UserId, string? Role, string? Period, bool? MineOnly, string? Action)
    : IRequest<IReadOnlyList<PrimeFicheValidationHistoryFeedItemDto>>;

public sealed class GetSynthesisTrackingFeedQueryHandler(IPrimeGlobalPoolScopeAppService scope)
    : IRequestHandler<GetSynthesisTrackingFeedQuery, IReadOnlyList<PrimeFicheValidationHistoryFeedItemDto>>
{
    public Task<IReadOnlyList<PrimeFicheValidationHistoryFeedItemDto>> Handle(
        GetSynthesisTrackingFeedQuery request, CancellationToken ct) =>
        scope.GetSynthesisTrackingFeedAsync(
            request.UserId, request.Role, request.Period, request.MineOnly, request.Action, ct);
}

public record GetSynthesisLineHistoryQuery(Guid LineId, string? UserId, string? Role)
    : IRequest<IReadOnlyList<GlobalPoolSynthesisLineHistoryDto>>;

public sealed class GetSynthesisLineHistoryQueryHandler(IPrimeGlobalPoolScopeAppService scope)
    : IRequestHandler<GetSynthesisLineHistoryQuery, IReadOnlyList<GlobalPoolSynthesisLineHistoryDto>>
{
    public Task<IReadOnlyList<GlobalPoolSynthesisLineHistoryDto>> Handle(
        GetSynthesisLineHistoryQuery request, CancellationToken ct) =>
        scope.GetSynthesisLineHistoryAsync(request.LineId, request.UserId, request.Role, ct);
}

public record SetGlobalSynthesisLinePaymentCommand(Guid LineId, SetSynthesisLinePaymentRequest Body) : IRequest<Unit>;

public sealed class SetGlobalSynthesisLinePaymentCommandHandler(IPrimeGlobalPoolScopeAppService scope)
    : IRequestHandler<SetGlobalSynthesisLinePaymentCommand, Unit>
{
    public async Task<Unit> Handle(SetGlobalSynthesisLinePaymentCommand request, CancellationToken ct)
    {
        await scope.SetLinePaymentAsync(request.LineId, request.Body, ct);
        return Unit.Value;
    }
}

public record PayAllGlobalSynthesisCommand(Guid ScopeSynthesisId, PaySynthesisAllRequest Body) : IRequest<Unit>;

public sealed class PayAllGlobalSynthesisCommandHandler(IPrimeGlobalPoolScopeAppService scope)
    : IRequestHandler<PayAllGlobalSynthesisCommand, Unit>
{
    public async Task<Unit> Handle(PayAllGlobalSynthesisCommand request, CancellationToken ct)
    {
        await scope.PayAllAsync(request.ScopeSynthesisId, request.Body, ct);
        return Unit.Value;
    }
}

public record ListGlobalPoolPeriodsQuery : IRequest<IReadOnlyList<string>>;

public sealed class ListGlobalPoolPeriodsQueryHandler(IPrimeGlobalPoolScopeAppService scope)
    : IRequestHandler<ListGlobalPoolPeriodsQuery, IReadOnlyList<string>>
{
    public Task<IReadOnlyList<string>> Handle(ListGlobalPoolPeriodsQuery request, CancellationToken ct) =>
        scope.ListPeriodsAsync(ct);
}
