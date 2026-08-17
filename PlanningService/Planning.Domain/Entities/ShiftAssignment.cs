using Planning.Domain.Enums;
using Planning.Domain.Entities;

public class ShiftAssignment
{
    public int Id { get; set; }
    public int WeeklyPlanningId { get; set; }
    public WeeklyPlanning WeeklyPlanning { get; set; }
    public int UserId { get; set; }
    public User User { get; set; }
    public int? ShiftId { get; set; }
    public Shift? Shift { get; set; }
    public DateOnly AssignedDate { get; set; }
    public DayOfWeekEnum DayOfWeek { get; set; }

    // ? NOUVEAU
    public bool IsSaturday { get; set; } = false;
    public bool IsNewEmployee { get; set; } = false;
    public bool IsManagerOverride { get; set; } = false; // manager a modifi� manuellement
    /// <summary>Shift issu d'une demande exceptionnelle approuv�e (pin � la g�n�ration).</summary>
    public bool IsExceptionalRequest { get; set; } = false;
    /// <summary>Shift issu d'un renfort samedi (n'impacte pas SaturdayHistory).</summary>
    public bool IsReinforcement { get; set; } = false;
    public TimeOnly? BreakTime { get; set; }  // ex: 12:00, 13:00, 14:00
    public bool IsOnLeave { get; set; } = false;  // ? En conge
    public bool IsHalfDaySaturday { get; set; } = false; // ? Nouveau employe 4h
    public int SaturdaySlot { get; set; } = 0;
    public bool IsHoliday { get; set; } = false;

    public ICollection<Declaration> Declarations { get; set; }
    // ? NOUVEAU � FK vers SubServiceShiftConfig
    public int? SubServiceShiftConfigId { get; set; }
    public SubServiceShiftConfig? SubServiceShiftConfig { get; set; }

    /// <summary>Mode effectivement travaillé ce jour (peut différer du mode hebdomadaire après switch).</summary>
    public int? ShiftModeProfileId { get; set; }
    public ShiftModeProfile? ShiftModeProfile { get; set; }

    /// <summary>True si le mode du jour vient d’un switch approuvé (override protégé à la regen).</summary>
    public bool IsModeOverride { get; set; }
}