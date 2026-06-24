using Prime.Application.DTOs;

namespace Prime.Application.Abstractions;

public interface IPrimeCelluleDraftGlobalPoolAppService
{
    Task<CelluleDraftGlobalPoolStateDto> GetStateAsync(
        Guid draftId,
        string supervisorUserId,
        CancellationToken ct = default);

    Task<FileExportResultDto> DownloadExcelAsync(
        Guid draftId,
        string supervisorUserId,
        string? actingUserId,
        CancellationToken ct = default);

    Task<CelluleDraftGlobalPoolStateDto> GenerateLegacyExcelAsync(
        Guid draftId,
        string supervisorUserId,
        CancellationToken ct = default);

    Task<CelluleDraftGlobalPoolStateDto> ApproveManagerAsync(
        Guid draftId,
        string supervisorUserId,
        GlobalPoolActingUserRequest body,
        CancellationToken ct = default);

    Task<CelluleDraftGlobalPoolStateDto> ApproveRhAsync(
        Guid draftId,
        string supervisorUserId,
        GlobalPoolActingUserRequest body,
        CancellationToken ct = default);

    Task<CelluleDraftGlobalPoolStateDto> AckComptaAsync(
        Guid draftId,
        string supervisorUserId,
        GlobalPoolActingUserRequest body,
        CancellationToken ct = default);
}
