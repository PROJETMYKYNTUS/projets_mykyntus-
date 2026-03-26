namespace PlanningService.Models;

public class SaturdayHistory
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int SubServiceId { get; set; }
    public string WeekCode { get; set; } = string.Empty; // "2026-W13"
    public bool WorkedSaturday { get; set; } // true = travaillé, false = OFF
    public bool IsManualEntry { get; set; } = false; // true = saisi manuellement
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}