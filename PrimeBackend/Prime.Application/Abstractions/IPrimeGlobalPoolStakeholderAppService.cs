using Prime.Application.DTOs;

namespace Prime.Application.Abstractions;

public interface IPrimeGlobalPoolStakeholderAppService
{
    Task<IReadOnlyList<GlobalPoolInboxItemDto>> GetInboxAsync(
        string userId,
        string? role,
        CancellationToken ct = default);

    Task<FileExportResultDto> DownloadExcelAsync(Guid draftId, string userId, CancellationToken ct = default);

    Task<CelluleDraftGlobalPoolStateDto> ApproveStepAsync(
        Guid draftId,
        GlobalPoolApproveStepRequest body,
        CancellationToken ct = default);

    Task<CelluleDraftGlobalPoolStateDto> ApproveManagerAsync(
        Guid draftId,
        GlobalPoolActingUserRequest body,
        CancellationToken ct = default);

    Task<CelluleDraftGlobalPoolStateDto> ApproveRhAsync(
        Guid draftId,
        GlobalPoolActingUserRequest body,
        CancellationToken ct = default);

    Task<CelluleDraftGlobalPoolStateDto> AckComptaAsync(
        Guid draftId,
        GlobalPoolActingUserRequest body,
        CancellationToken ct = default);
}
