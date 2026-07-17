using Planning.Application.DTOs.Planning;

namespace Planning.Application.Abstractions;

public interface IPlanningChangeRequestService
{
    Task<PlanningChangeRequestDto> CreateAsync(int requesterUserId, CreatePlanningChangeRequestDto dto);
    Task<List<PlanningChangeRequestDto>> GetMyAsync(int requesterUserId);
    Task<List<PlanningChangeRequestDto>> GetAllAsync(string? status, string? weekCode);
    Task<List<ChangeRequestStatsByEmployeeDto>> GetStatsByEmployeeAsync(string? weekCode);
    Task<List<SwapCandidateDto>> GetSwapCandidatesAsync(int assignmentId, int requesterUserId);
    Task<PlanningChangeRequestDto> ApproveAsync(int id, int processedByUserId);
    Task<PlanningChangeRequestDto> RejectAsync(int id, int processedByUserId, string? reason);
    Task<PlanningChangeRequestDto> CancelAsync(int id, int requesterUserId);
}
