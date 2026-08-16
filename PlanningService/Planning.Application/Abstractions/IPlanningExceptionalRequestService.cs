using Planning.Application.DTOs.Planning;

namespace Planning.Application.Abstractions;

public interface IPlanningExceptionalRequestService
{
    Task<PlanningExceptionalRequestDto> CreateAsync(
        int requesterUserId,
        DateOnly requestedDate,
        int requestedShiftTemplateId,
        string reason,
        Stream? justificationStream,
        string? justificationFileName,
        string? justificationContentType);

    Task<List<PlanningExceptionalRequestDto>> GetMyAsync(int requesterUserId);

    Task<List<PlanningExceptionalRequestDto>> GetAllAsync(
        string? status,
        string? weekCode,
        int? viewerUserId = null,
        int? requesterUserId = null,
        DateOnly? from = null,
        DateOnly? to = null);

    Task<ExceptionalRequestQuotaDto> GetQuotaAsync(int requesterUserId, int? year = null);

    Task<List<ExceptionalShiftOptionDto>> GetAvailableShiftsAsync(int requesterUserId);

    Task<ExceptionalRequestTargetWeekDto> GetTargetWeekAsync(DateTime? utcNow = null);

    Task<PlanningExceptionalRequestDto> SupervisorApproveAsync(int id, int processedByUserId);

    Task<PlanningExceptionalRequestDto> SupervisorRejectAsync(int id, int processedByUserId, string? reason);

    Task<PlanningExceptionalRequestDto> RhApproveAsync(int id, int processedByUserId);

    Task<PlanningExceptionalRequestDto> RhRejectAsync(int id, int processedByUserId, string? reason);

    Task<PlanningExceptionalRequestDto> CancelAsync(int id, int requesterUserId);

    Task<(byte[] Content, string ContentType, string FileName)?> GetJustificationAsync(int id, int viewerUserId);
}
