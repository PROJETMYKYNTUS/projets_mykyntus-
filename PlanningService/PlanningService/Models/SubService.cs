namespace PlanningService.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class SubService
{
    public int Id { get; set; }

    [ForeignKey("Service")]
    public int ServiceId { get; set; }
    public Service Service { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; }

    [Required]
    [MaxLength(20)]
    public string Code { get; set; }

    public ICollection<User> Users { get; set; }
    public ICollection<WeeklyPlanning> WeeklyPlannings { get; set; }
    public ICollection<UserSubService> Managers { get; set; } = new List<UserSubService>();
}
