using Prime.Application.DTOs;

namespace Prime.Application.Abstractions;

public sealed record GenerateSynthesisResultDto(
    Guid ScopeSynthesisId,
    string? FileName,
    DateTimeOffset? GeneratedAt);

public sealed record EnsureSynthesisResultDto(
    Guid? ScopeSynthesisId,
    bool Ready,
    string? FileName,
    DateTimeOffset? GeneratedAt,
    string? Error);

public interface IPrimeGlobalPoolScopeAppService
{
    Task<GlobalPoolReadinessDto> GetReadinessAsync(string period, CancellationToken ct = default);

    Task<GlobalSynthesisLinesResponseDto> GetSynthesisLinesAsync(
        string period,
        string scopeType,
        string scopeId,
        Guid? scopeSynthesisId,
        string? userId,
        CancellationToken ct = default);

    Task<GlobalSynthesisSummaryDto> GetSynthesisSummaryAsync(
        string period,
        string scopeType,
        string scopeId,
        Guid? scopeSynthesisId,
        CancellationToken ct = default);

    Task<GenerateSynthesisResultDto> GenerateSynthesisAsync(
        GenerateScopeSynthesisRequest body,
        CancellationToken ct = default);

    Task<EnsureSynthesisResultDto> EnsureSynthesisAsync(
        GenerateScopeSynthesisRequest body,
        CancellationToken ct = default);

    Task<IReadOnlyList<GlobalPoolScopeSynthesisInboxItemDto>> GetScopeInboxAsync(
        string userId,
        string? role,
        CancellationToken ct = default);

    Task<FileExportResultDto> DownloadScopeExcelAsync(Guid scopeSynthesisId, string userId, CancellationToken ct = default);

    Task<GlobalPoolScopeSynthesisInboxItemDto> ApproveScopeStepAsync(
        Guid scopeSynthesisId,
        GlobalPoolApproveStepRequest body,
        CancellationToken ct = default);

    Task<GlobalPoolScopeSynthesisInboxItemDto> ApproveScopeManagerAsync(
        Guid scopeSynthesisId,
        GlobalPoolActingUserRequest body,
        CancellationToken ct = default);

    Task<GlobalPoolScopeSynthesisInboxItemDto> ApproveScopeRhAsync(
        Guid scopeSynthesisId,
        GlobalPoolActingUserRequest body,
        CancellationToken ct = default);

    Task<GlobalPoolScopeSynthesisInboxItemDto> AckScopeComptaAsync(
        Guid scopeSynthesisId,
        GlobalPoolActingUserRequest body,
        CancellationToken ct = default);

    Task RejectLineAsync(Guid lineId, RejectSynthesisLineRequest body, CancellationToken ct = default);

    Task ApproveLineAsync(Guid lineId, GlobalPoolActingUserRequest body, CancellationToken ct = default);

    Task<IReadOnlyList<SupervisorSynthesisTrackingItemDto>> GetSupervisorSynthesisTrackingAsync(
        string supervisorUserId,
        string? period,
        CancellationToken ct = default);

    Task<IReadOnlyList<EmployeePrimePaymentTrackingDto>> GetMySynthesisTrackingAsync(
        string? userId,
        string? role,
        CancellationToken ct = default);

    Task<IReadOnlyList<PrimeFicheValidationHistoryFeedItemDto>> GetSynthesisTrackingFeedAsync(
        string? userId,
        string? role,
        string? period,
        bool? mineOnly,
        string? action,
        CancellationToken ct = default);

    Task<IReadOnlyList<GlobalPoolSynthesisLineHistoryDto>> GetSynthesisLineHistoryAsync(
        Guid lineId,
        string? userId,
        string? role,
        CancellationToken ct = default);

    Task SetLinePaymentAsync(Guid lineId, SetSynthesisLinePaymentRequest body, CancellationToken ct = default);

    Task PayAllAsync(Guid scopeSynthesisId, PaySynthesisAllRequest body, CancellationToken ct = default);

    Task UpdateLineAdjustmentsAsync(
        Guid lineId,
        UpdateSynthesisLineAdjustmentsRequest body,
        CancellationToken ct = default);

    Task<IReadOnlyList<string>> ListPeriodsAsync(CancellationToken ct = default);
}
