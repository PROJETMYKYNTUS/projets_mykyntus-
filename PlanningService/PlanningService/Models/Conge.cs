using PlanningService.Enums;

namespace PlanningService.Models;

public class Conge
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public CongeStatus Status { get; set; } = CongeStatus.Approved;
    public AbsenceType AbsenceType { get; set; } = AbsenceType.CongesPayes; // ← NOUVEAU
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
}