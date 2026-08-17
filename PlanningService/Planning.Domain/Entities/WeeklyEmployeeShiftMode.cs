using System.ComponentModel.DataAnnotations.Schema;

namespace Planning.Domain.Entities;

/// <summary>Mode hebdomadaire unique d’un employé pour une cellule / semaine.</summary>
public class WeeklyEmployeeShiftMode
{
    public int Id { get; set; }

    [ForeignKey(nameof(Plan))]
    public int WeeklyCellShiftModePlanId { get; set; }
    public WeeklyCellShiftModePlan Plan { get; set; } = null!;

    [ForeignKey(nameof(User))]
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    [ForeignKey(nameof(ShiftModeProfile))]
    public int ShiftModeProfileId { get; set; }
    public ShiftModeProfile ShiftModeProfile { get; set; } = null!;
}
