namespace PlanningService.Models;

public class WeeklyShiftConfig
{
    public int Id { get; set; }
    public int WeeklyPlanningId { get; set; }
    public WeeklyPlanning WeeklyPlanning { get; set; }
    public int ShiftId { get; set; }
    public Shift Shift { get; set; }

    // ✅ NOUVEAU — nombre calculé depuis TotalEffectif
    public int RequiredCount { get; set; }

    // Garder le % résultant pour affichage
    public decimal Percentage { get; set; }
    public bool IsManuallySet { get; set; } = false;
}