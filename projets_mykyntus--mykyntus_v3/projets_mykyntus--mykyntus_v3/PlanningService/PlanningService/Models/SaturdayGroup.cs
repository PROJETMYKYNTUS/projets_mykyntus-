namespace PlanningService.Models;

public class SaturdayGroup
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    // Groupe 1 ou 2 (rotation semaine paire/impaire)
    public int GroupNumber { get; set; }

    // ✅ Manager décide si l'employé est "nouveau" → tous les samedis
    public bool IsNewEmployee { get; set; } = false;

    // Manager peut override la rotation pour une semaine spécifique
    public bool ManagerOverride { get; set; } = false;
    public int NewEmployeeSlot { get; set; } = 0;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public int AssignedBy { get; set; } // UserId du manager
   
}