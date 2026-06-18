using PlanningService.Enums;
using PlanningService.Models;

public class WeeklyPlanning
{
    public int Id { get; set; }
    public int SubServiceId { get; set; }
    public SubService SubService { get; set; }
    public string WeekCode { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public PlanningStatus Status { get; set; } = PlanningStatus.Draft;

    // ✅ NOUVEAU — effectif total voulu par le manager (Option B)
    public int TotalEffectif { get; set; } = 0;

    // ✅ NOUVEAU — groupe samedi actif cette semaine (1 ou 2)
    public int SaturdayGroupId { get; set; } = 1;

    public int? ValidatedBy { get; set; }
    public User? Validator { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ShiftAssignment> ShiftAssignments { get; set; }
    public ICollection<WeeklyShiftConfig> WeeklyShiftConfigs { get; set; }
}