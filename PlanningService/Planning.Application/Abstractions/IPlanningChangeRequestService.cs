using Planning.Application.DTOs.Planning;

namespace Planning.Application.Abstractions;

public interface IPlanningChangeRequestService
{
    Task<PlanningChangeRequestDto> CreateAsync(int requesterUserId, CreatePlanningChangeRequestDto dto);
    Task<List<PlanningChangeRequestDto>> GetMyAsync(int requesterUserId);
    Task<List<PlanningChangeRequestDto>> GetAllAsync(
        string? status,
        string? weekCode,
        int? viewerUserId = null,
        int? requesterUserId = null,
        DateOnly? from = null,
        DateOnly? to = null);
    Task<List<ChangeRequestStatsByEmployeeDto>> GetStatsByEmployeeAsync(
        string? weekCode, DateOnly? from = null, DateOnly? to = null);
    Task<List<SwapCandidateDto>> GetSwapCandidatesAsync(int assignmentId, int requesterUserId);
    Task<PlanningChangeRequestDto> PartnerAcceptAsync(int id, int partnerUserId);
    Task<PlanningChangeRequestDto> PartnerRejectAsync(int id, int partnerUserId, string? reason);
    Task<PlanningChangeRequestDto> ApproveAsync(int id, int processedByUserId);
    Task<PlanningChangeRequestDto> RejectAsync(int id, int processedByUserId, string? reason);
    Task<PlanningChangeRequestDto> CancelAsync(int id, int requesterUserId);
}
