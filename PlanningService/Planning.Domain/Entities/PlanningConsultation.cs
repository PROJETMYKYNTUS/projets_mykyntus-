namespace Planning.Domain.Entities;

/// <summary>Preuve qu'un utilisateur a consulté un planning avant validation.</summary>
public class PlanningConsultation
{
    public int Id { get; set; }
    public int PlanningId { get; set; }
    public WeeklyPlanning Planning { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime ConsultedAt { get; set; } = DateTime.UtcNow;
}
