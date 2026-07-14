namespace Planning.Application.DTOs.Planning;

// -- Cr�er un planning --
public class CreateWeeklyPlanningDto
{
    public int SubServiceId { get; set; }
    public string WeekCode { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public int TotalEffectif { get; set; }
}

// -- G�n�rer le planning automatiquement --
public class GeneratePlanningDto
{
    public int WeeklyPlanningId { get; set; }
    public int TotalEffectif { get; set; }
}

// -- Override manuel d'un shift par le manager --
public class OverrideShiftDto
{
    public int ShiftAssignmentId { get; set; }
    public int NewShiftId { get; set; }                    // ancien syst�me (gard� pour compatibilit�)
    public int NewSubServiceShiftConfigId { get; set; }    // ? nouveau syst�me
}

// -- Configurer groupe samedi --
public class SetSaturdayGroupDto
{
    public int UserId { get; set; }
    public int GroupNumber { get; set; }
    public bool IsNewEmployee { get; set; } = false;
}

public record SetSaturdayHistoryDto(
    int SubServiceId,
    string WeekCode,
    List<SaturdayHistoryEntryDto> Entries
);

public record SaturdayHistoryEntryDto(
    int UserId,
    bool WorkedSaturday
);

public record SaturdayHistoryResponseDto(
    int UserId,
    string FullName,
    string WeekCode,
    bool WorkedSaturday,
    bool IsManualEntry
);

// -- Réponse planning complet --
public class WeeklyPlanningResponseDto
{
    public int Id { get; set; }
    public string WeekCode { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TotalEffectif { get; set; }
    public int SaturdayGroupId { get; set; }
    public string SubServiceName { get; set; } = string.Empty;
    public List<ShiftConfigResponseDto> ShiftConfigs { get; set; } = new();
    public int SubServiceId { get; set; }
    public List<EmployeePlanningDto> Assignments { get; set; } = new();
    public CoverageReportDto? CoverageReport { get; set; }
}

public class CoverageReportDto
{
    public bool HasUnderstaffing { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<CoverageDayShiftDto> Items { get; set; } = new();
}

public class CoverageDayShiftDto
{
    public DateOnly Date { get; set; }
    public string Day { get; set; } = string.Empty;
    public int ShiftConfigId { get; set; }
    public string ShiftLabel { get; set; } = string.Empty;
    public int RequiredCount { get; set; }
    public int AssignedCount { get; set; }
    public int MinPresencePercent { get; set; }
    public decimal PresencePercent { get; set; }
    public bool IsUnderstaffed { get; set; }
}

public record SaturdayYtdDto(
    int UserId,
    string FullName,
    int WorkedCount,
    int OffCount,
    int TotalWeeksRecorded,
    decimal WorkedPercent
);

// ? CORRIG� � WeeklyPlanningId + UserId ajout�s pour cr�er une assignation quand l'employ� est OFF
public class OverrideSaturdayDto
{
    public int ShiftAssignmentId { get; set; }          // 0 si l'employ� est actuellement OFF
    public int NewSubServiceShiftConfigId { get; set; }
    public int WeeklyPlanningId { get; set; }           // requis quand ShiftAssignmentId = 0
    public int UserId { get; set; }                     // requis quand ShiftAssignmentId = 0
}

// -- Config shift (% et count) --
public class ShiftConfigResponseDto
{
    public int ShiftId { get; set; }
    public string ShiftLabel { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public int RequiredCount { get; set; }
    public decimal Percentage { get; set; }
}

// -- Shift d'un jour --
public class DayAssignmentDto
{
    public int AssignmentId { get; set; }
    public string Day { get; set; } = string.Empty;
    public DateOnly AssignedDate { get; set; }
    public string ShiftLabel { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public bool IsSaturday { get; set; }
    public bool IsManagerOverride { get; set; }
    public string EndTime { get; set; } = string.Empty;
    public string? BreakTime { get; set; }
    public bool IsOnLeave { get; set; }
    public bool IsHalfDaySaturday { get; set; }
    public bool IsHoliday { get; set; } = false;
    public string HolidayName { get; set; } = string.Empty;
    public int SaturdaySlot { get; set; }
    public string? AbsenceType { get; set; } // ? AJOUTER
    public string SlotLabel { get; set; } = string.Empty;
}

// -- Vue employ� (son propre planning) --
public class MyPlanningDto
{
    public string WeekCode { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public string SubServiceName { get; set; } = string.Empty;
    public List<DayAssignmentDto> Days { get; set; } = new();
}

// -- Notification de publication de planning (persist�e) --
public class PlanningNotificationDto
{
    public int Id { get; set; }
    public int? WeeklyPlanningId { get; set; }
    public string WeekCode { get; set; } = string.Empty;
    public string SubServiceName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

// -- Sauvegarder la config shifts (template si WeekCode vide) --
public class SaveShiftConfigDto
{
    public int SubServiceId { get; set; }
    /// <summary>Vide = sauvegarde du modèle permanent.</summary>
    public string? WeekCode { get; set; }
    public DateOnly? WeekStartDate { get; set; }
    public List<ShiftConfigItemDto> Shifts { get; set; } = new();
}

// -- Un shift dans la config --
public class ShiftConfigItemDto
{
    public string Label { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public int WorkHours { get; set; } = 8;
    public int BreakDurationMinutes { get; set; } = 60;
    public string? BreakRangeStart { get; set; }
    public string? BreakRangeEnd { get; set; }
    public int RequiredCount { get; set; }
    public int MinPresencePercent { get; set; } = 70;
    public int DisplayOrder { get; set; }
}

// -- Réponse après sauvegarde config --
public class ShiftConfigResponseNewDto
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public int WorkHours { get; set; }
    public string BreakRangeStart { get; set; } = string.Empty;
    public string BreakRangeEnd { get; set; } = string.Empty;
    public int BreakDurationMinutes { get; set; }
    public int RequiredCount { get; set; }
    public decimal Percentage { get; set; }
    public int MinPresencePercent { get; set; }
    public int DisplayOrder { get; set; }
}

// -- Statut modèle shift par service (vue arbre RH) --
public class ShiftConfigStatusItemDto
{
    public int SubServiceId { get; set; }
    public string SubServiceName { get; set; } = string.Empty;
    public string? PrimeServiceId { get; set; }
    public bool HasTemplate { get; set; }
    public int ShiftCount { get; set; }
    public int TemplateEffectif { get; set; }
    public int ActiveEmployeeCount { get; set; }
}

public class ShiftConfigStatusResponseDto
{
    public List<ShiftConfigStatusItemDto> Items { get; set; } = new();
    public int ConfiguredCount { get; set; }
    public int TotalCount { get; set; }
}

// -- Réponse config complète (template ou snapshot) --
public class WeekShiftConfigResponseDto
{
    public int SubServiceId { get; set; }
    public string SubServiceName { get; set; } = string.Empty;
    public string WeekCode { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public bool IsTemplate { get; set; }
    public int TotalEffectif { get; set; }
    public List<ShiftConfigResponseNewDto> Shifts { get; set; } = new();
}

// -- Générer le planning depuis la config --
public class GeneratePlanningFromConfigDto
{
    public int SubServiceId { get; set; }
    public string? WeekCode { get; set; }
    public int WeeklyPlanningId { get; set; }
}

public class PlanningWeekItemDto
{
    public int SubServiceId { get; set; }
    public string SubServiceName { get; set; } = string.Empty;
    public string OrgLabel { get; set; } = string.Empty;
    public int? PlanningId { get; set; }
    public string? Status { get; set; }
    public int TotalEffectif { get; set; }
    public bool HasTemplate { get; set; }
    public bool CoverageOk { get; set; } = true;
    public bool HasConsulted { get; set; }
}

public class PlanningWeekListDto
{
    public string WeekCode { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public List<PlanningWeekItemDto> Items { get; set; } = new();
}

public class AutoGenerateSettingsDto
{
    public bool Enabled { get; set; } = true;
    public int DayOfWeek { get; set; } = 4;
    public int HourLocal { get; set; } = 6;
    public int MinuteLocal { get; set; }
    public string TimeZone { get; set; } = "Africa/Casablanca";
    public string Target { get; set; } = "NextWeek";
    public DateTime? LastRunAt { get; set; }
    public string? LastRunWeekCode { get; set; }
}

public class AutoGenerateWeekResultDto
{
    public string WeekCode { get; set; } = string.Empty;
    public int Created { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public List<string> Messages { get; set; } = new();
}

public class OverrideBreakDto
{
    public int ShiftAssignmentId { get; set; }
    public string NewBreakTime { get; set; } = string.Empty;
}

// -- Sauvegarder un commentaire --
public class SavePlanningCommentDto
{
    public int WeeklyPlanningId { get; set; }
    public int UserId { get; set; }
    public string Comment { get; set; } = string.Empty;
    public int CreatedBy { get; set; }
}

// -- Réponse commentaire --
public class PlanningCommentDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class EmployeePlanningDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public bool IsNewEmployee { get; set; }
    public int Level { get; set; }
    public List<DayAssignmentDto> Days { get; set; } = new();
    public string? ManagerComment { get; set; }
}