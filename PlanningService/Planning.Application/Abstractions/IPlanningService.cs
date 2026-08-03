using Planning.Application.DTOs.Planning;
namespace Planning.Application.Abstractions;

public interface IPlanningService
{
    // -- CRUD Planning --
    Task<WeeklyPlanningResponseDto> CreatePlanningAsync(CreateWeeklyPlanningDto dto);
    Task<WeeklyPlanningResponseDto?> GetPlanningByIdAsync(int id);
    Task<IEnumerable<WeeklyPlanningResponseDto>> GetPlanningsBySubServiceAsync(int subServiceId);
    Task DeletePlanningAsync(int id);

    // -- Génération ancienne (compatibilité) --
    Task<WeeklyPlanningResponseDto> GeneratePlanningAsync(GeneratePlanningDto dto);
    Task AutoAssignSaturdayGroupsAsync(int subServiceId);

    // -- Config shifts : template + snapshot --
    Task<WeekShiftConfigResponseDto> SaveShiftConfigAsync(SaveShiftConfigDto dto);
    Task<WeekShiftConfigResponseDto?> GetShiftConfigAsync(int subServiceId, string weekCode);
    Task<WeekShiftConfigResponseDto?> GetShiftTemplateAsync(int subServiceId);
    Task<WeekShiftConfigResponseDto> SaveShiftTemplateAsync(SaveShiftConfigDto dto);
    Task<ShiftConfigStatusResponseDto> GetShiftConfigStatusAsync();
    Task EnsureWeekSnapshotAsync(int subServiceId, string weekCode, DateOnly weekStartDate, bool forceRefresh = false);

    // -- Génération depuis config / auto --
    Task<WeeklyPlanningResponseDto> GeneratePlanningFromConfigAsync(GeneratePlanningFromConfigDto dto);
    Task<AutoGenerateWeekResultDto> AutoGenerateWeekAsync(string? weekCode = null, bool forceDraftRefresh = false);
    Task<AutoGenerateSettingsDto> GetAutoGenerateSettingsAsync();
    Task<AutoGenerateSettingsDto> SaveAutoGenerateSettingsAsync(AutoGenerateSettingsDto dto, int? updatedByUserId);

    // -- Validation RH/Admin --
    Task<PlanningWeekListDto> GetWeekOverviewAsync(string weekCode, int? viewerUserId = null);
    Task RecordConsultationAsync(int planningId, int userId);
    Task<bool> HasConsultedAsync(int planningId, int userId);

    // -- Publication --
    Task<WeeklyPlanningResponseDto> PublishPlanningAsync(int planningId, int validatorId);

    // -- Override manager --
    Task<DayAssignmentDto> OverrideShiftAsync(OverrideShiftDto dto);
    Task<DayAssignmentDto> OverrideBreakAsync(OverrideBreakDto dto);
    Task<MyPlanningDto?> GetMyCurrentPlanningAsync(int userId);

    // -- Samedi --
    Task SetSaturdayGroupAsync(SetSaturdayGroupDto dto);
    Task<IEnumerable<object>> GetSaturdayGroupsAsync(int subServiceId);
    Task SyncNewEmployeesAsync();

    // -- Vue employé --
    Task<MyPlanningDto?> GetMyPlanningAsync(int userId, string weekCode);
    Task<IEnumerable<MyPlanningDto>> GetMyPlanningHistoryAsync(int userId);
    Task<PlanningCommentDto> SaveCommentAsync(SavePlanningCommentDto dto);
    Task DeleteCommentAsync(int planningId, int userId);
    Task<IEnumerable<PlanningCommentDto>> GetCommentsAsync(int planningId);
    Task<List<SaturdayHistoryResponseDto>> GetSaturdayHistoryAsync(int subServiceId, string weekCode);
    Task<List<SaturdayYtdDto>> GetSaturdayYtdAsync(int subServiceId, int year);
    Task SaveSaturdayHistoryAsync(SetSaturdayHistoryDto dto, bool isManual);
    Task<DayAssignmentDto> OverrideSaturdayShiftAsync(OverrideSaturdayDto dto);
    Task SetSaturdayOffAsync(int weeklyPlanningId, int userId);

    Task<IReadOnlyList<EquipePlanningSummaryDto>> GetEquipePlanningsByAuthUserIdAsync(int authUserId);

    // -- Notifications --
    Task<IEnumerable<PlanningNotificationDto>> GetMyNotificationsAsync(int authUserId);
    Task MarkNotificationReadAsync(int id, int authUserId);
    Task MarkAllNotificationsReadAsync(int authUserId);
}
