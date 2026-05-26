// PlanningService/Models/UserManagedService.cs
namespace PlanningService.Models;

public class UserManagedService
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int ServiceId { get; set; }
    public Service Service { get; set; } = null!;
}