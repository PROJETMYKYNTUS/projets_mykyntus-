// Models/PlanningComment.cs
namespace PlanningService.Models;

public class PlanningComment
{
    public int Id { get; set; }

    public int WeeklyPlanningId { get; set; }
    public WeeklyPlanning WeeklyPlanning { get; set; } = null!;

    public int UserId { get; set; }          // l'employé concerné
    public User User { get; set; } = null!;

    public string Comment { get; set; } = string.Empty;

    public int CreatedBy { get; set; }       // id du responsable
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}