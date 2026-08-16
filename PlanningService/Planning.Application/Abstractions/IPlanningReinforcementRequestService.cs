using Planning.Application.DTOs.Planning;

namespace Planning.Application.Abstractions;

public interface IPlanningReinforcementRequestService
{
    Task<PlanningReinforcementRequestDto> CreateAsync(
        int createdByUserId, CreatePlanningReinforcementRequestDto dto);

    Task<List<PlanningReinforcementRequestDto>> GetAllAsync(
        string? status,
        string? weekCode,
        int? viewerUserId = null,
        DateOnly? from = null,
        DateOnly? to = null);

    Task<List<ReinforcementContributorStatsDto>> GetContributorStatsAsync(
        int? viewerUserId = null,
        DateOnly? from = null,
        DateOnly? to = null,
        int? subServiceId = null);

    Task<PlanningReinforcementRequestDto?> GetByIdAsync(int id, int? viewerUserId = null);

    Task<List<PlanningReinforcementRequestDto>> GetMyAsync(int planningUserId);

    Task<PlanningReinforcementRequestDto> VolunteerAcceptAsync(int id, int planningUserId);

    Task<PlanningReinforcementRequestDto> VolunteerDeclineAsync(int id, int planningUserId);

    Task<PlanningReinforcementRequestDto> SelectAsync(
        int id, int processedByUserId, SelectReinforcementVolunteersDto dto);

    Task<PlanningReinforcementRequestDto> CancelAsync(int id, int processedByUserId);
}
