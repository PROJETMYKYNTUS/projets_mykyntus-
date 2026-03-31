namespace PlanningService.DTOs.Planning;

// ── Créer un planning ──
public class CreateWeeklyPlanningDto
{
    public int SubServiceId { get; set; }
    public string WeekCode { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public int TotalEffectif { get; set; }
}

// ── Générer le planning automatiquement ──
public class GeneratePlanningDto
{
    public int WeeklyPlanningId { get; set; }
    public int TotalEffectif { get; set; }
}

// ── Override manuel d'un shift par le manager ──
public class OverrideShiftDto
{
    public int ShiftAssignmentId { get; set; }
    public int NewShiftId { get; set; }                    // ancien système (gardé pour compatibilité)
    public int NewSubServiceShiftConfigId { get; set; }    // ✅ nouveau système
}

// ── Configurer groupe samedi ──
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

// ── Réponse planning complet ──
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
}

// ✅ CORRIGÉ — WeeklyPlanningId + UserId ajoutés pour créer une assignation quand l'employé est OFF
public class OverrideSaturdayDto
{
    public int ShiftAssignmentId { get; set; }          // 0 si l'employé est actuellement OFF
    public int NewSubServiceShiftConfigId { get; set; }
    public int WeeklyPlanningId { get; set; }           // requis quand ShiftAssignmentId = 0
    public int UserId { get; set; }                     // requis quand ShiftAssignmentId = 0
}

// ── Config shift (% et count) ──
public class ShiftConfigResponseDto
{
    public int ShiftId { get; set; }
    public string ShiftLabel { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public int RequiredCount { get; set; }
    public decimal Percentage { get; set; }
}

// ── Shift d'un jour ──
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
    public string? AbsenceType { get; set; } // ← AJOUTER
    public string SlotLabel { get; set; } = string.Empty;
}

// ── Vue employé (son propre planning) ──
public class MyPlanningDto
{
    public string WeekCode { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public string SubServiceName { get; set; } = string.Empty;
    public List<DayAssignmentDto> Days { get; set; } = new();
}

// ── Sauvegarder la config shifts d'une semaine ──
public class SaveShiftConfigDto
{
    public int SubServiceId { get; set; }
    public string WeekCode { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public List<ShiftConfigItemDto> Shifts { get; set; } = new();
}

// ── Un shift dans la config ──
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

// ── Réponse après sauvegarde config ──
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

// ── Réponse config complète d'une semaine ──
public class WeekShiftConfigResponseDto
{
    public int SubServiceId { get; set; }
    public string SubServiceName { get; set; } = string.Empty;
    public string WeekCode { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public int TotalEffectif { get; set; }
    public List<ShiftConfigResponseNewDto> Shifts { get; set; } = new();
}

// ── Générer le planning depuis la config ──
public class GeneratePlanningFromConfigDto
{
    public int SubServiceId { get; set; }
    public string WeekCode { get; set; } = string.Empty;
    public int WeeklyPlanningId { get; set; }
}

public class OverrideBreakDto
{
    public int ShiftAssignmentId { get; set; }
    public string NewBreakTime { get; set; } = string.Empty;
}

// ── Sauvegarder un commentaire ──
public class SavePlanningCommentDto
{
    public int WeeklyPlanningId { get; set; }
    public int UserId { get; set; }
    public string Comment { get; set; } = string.Empty;
    public int CreatedBy { get; set; }
}

// ── Réponse commentaire ──
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