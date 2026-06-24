using Prime.Application.DTOs;

namespace Prime.Application.Abstractions;

public interface IPrimeValidationAppService
{
    Task<ValidationReconcileResultDto> ReconcileReadyAsync(CancellationToken ct = default);

    Task<WorkflowValidationMetaDto> GetWorkflowMetaAsync(string? role, CancellationToken ct = default);

    Task<IReadOnlyList<EmployeePrimeServiceFicheValidationDto>> ListAsync(
        string? period,
        string? status,
        string? serviceId,
        string? celluleId,
        string? userId,
        string? role,
        bool? readyOnly,
        CancellationToken ct = default);

    Task<WorkflowValidationSummaryDto> GetSummaryAsync(
        string? period,
        string? serviceId,
        string? celluleId,
        string? userId,
        string? role,
        bool? readyOnly,
        CancellationToken ct = default);

    Task<IReadOnlyList<PrimeFicheValidationHistoryFeedItemDto>> GetHistoryFeedAsync(
        string? userId,
        string? role,
        string? period,
        bool? mineOnly,
        string? action,
        CancellationToken ct = default);

    Task<IReadOnlyList<string>> ListPeriodsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<PrimeFicheValidationHistoryDto>> GetFicheHistoryAsync(
        Guid id,
        string? userId,
        string? role,
        CancellationToken ct = default);

    Task<EmployeePrimeServiceFicheValidationDto> ApproveAsync(
        Guid id,
        ApproveServiceFicheRequest body,
        CancellationToken ct = default);

    Task<EmployeePrimeServiceFicheValidationDto> RejectAsync(
        Guid id,
        RejectServiceFicheRequest body,
        CancellationToken ct = default);

    Task<BulkApproveResultDto> BulkApproveAsync(BulkApproveServiceFicheRequest body, CancellationToken ct = default);

    Task<FileExportResultDto> ExportCsvAsync(Guid id, string? userId, string? role, CancellationToken ct = default);

    Task<FileExportResultDto> ExportXlsxAsync(Guid id, string? userId, string? role, CancellationToken ct = default);
}

public sealed record ValidationReconcileResultDto(
    int Reconciled,
    int DraftsValidated,
    int FichesEnsured,
    int ReconciledGlobal,
    int ReconciledByPeriod);

public sealed record BulkApproveResultDto(IReadOnlyList<Guid> ApprovedIds, IReadOnlyList<Guid> IgnoredIds);

public sealed record FileExportResultDto(byte[] Content, string ContentType, string FileName);
