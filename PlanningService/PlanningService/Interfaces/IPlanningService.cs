using PlanningService.DTOs.Planning;
namespace PlanningService.Interfaces;

public interface IPlanningService
{
    // ── CRUD Planning ──
    Task<WeeklyPlanningResponseDto> CreatePlanningAsync(CreateWeeklyPlanningDto dto);
    Task<WeeklyPlanningResponseDto?> GetPlanningByIdAsync(int id);
    Task<IEnumerable<WeeklyPlanningResponseDto>> GetPlanningsBySubServiceAsync(int subServiceId);
    Task DeletePlanningAsync(int id);

    // ── Génération ancienne (garder pour compatibilité) ──
    Task<WeeklyPlanningResponseDto> GeneratePlanningAsync(GeneratePlanningDto dto);
    Task AutoAssignSaturdayGroupsAsync(int subServiceId);

    // ✅ NOUVEAU — Config shifts par sous-service et par semaine ──
    Task<WeekShiftConfigResponseDto> SaveShiftConfigAsync(SaveShiftConfigDto dto);
    Task<WeekShiftConfigResponseDto?> GetShiftConfigAsync(int subServiceId, string weekCode);

    // ✅ NOUVEAU — Génération depuis la config ──
    Task<WeeklyPlanningResponseDto> GeneratePlanningFromConfigAsync(GeneratePlanningFromConfigDto dto);

    // ── Publication ──
    Task<WeeklyPlanningResponseDto> PublishPlanningAsync(int planningId, int validatorId);

    // ── Override manager ──
    Task<DayAssignmentDto> OverrideShiftAsync(OverrideShiftDto dto);
    Task<DayAssignmentDto> OverrideBreakAsync(OverrideBreakDto dto);
    Task<MyPlanningDto?> GetMyCurrentPlanningAsync(int userId);

    // ── Samedi ──
    Task SetSaturdayGroupAsync(SetSaturdayGroupDto dto);
    Task<IEnumerable<object>> GetSaturdayGroupsAsync(int subServiceId);
    Task SyncNewEmployeesAsync();

    // ── Vue employé ──
    Task<MyPlanningDto?> GetMyPlanningAsync(int userId, string weekCode);
    Task<IEnumerable<MyPlanningDto>> GetMyPlanningHistoryAsync(int userId);
    Task<PlanningCommentDto> SaveCommentAsync(SavePlanningCommentDto dto);
    Task DeleteCommentAsync(int planningId, int userId);
    Task<IEnumerable<PlanningCommentDto>> GetCommentsAsync(int planningId);
    Task<List<SaturdayHistoryResponseDto>> GetSaturdayHistoryAsync(int subServiceId, string weekCode);
    Task SaveSaturdayHistoryAsync(SetSaturdayHistoryDto dto, bool isManual);
    Task<DayAssignmentDto> OverrideSaturdayShiftAsync(OverrideSaturdayDto dto);
    Task SetSaturdayOffAsync(int weeklyPlanningId, int userId);
}