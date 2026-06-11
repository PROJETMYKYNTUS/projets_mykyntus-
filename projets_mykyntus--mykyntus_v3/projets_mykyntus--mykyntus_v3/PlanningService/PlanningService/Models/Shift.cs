namespace PlanningService.Models;

using System.ComponentModel.DataAnnotations;

public class Shift
{
    public int Id { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly LunchBreakTime { get; set; }

    [Required]
    [MaxLength(20)]
    public string Label { get; set; }

    public ICollection<ShiftAssignment> ShiftAssignments { get; set; }
}
