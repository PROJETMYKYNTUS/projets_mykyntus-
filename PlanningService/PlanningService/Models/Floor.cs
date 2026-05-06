namespace PlanningService.Models;

using System.ComponentModel.DataAnnotations;

public class Floor
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; }

    public int FloorNumber { get; set; }

    public string? Description { get; set; }

    public ICollection<Service> Services { get; set; }
}
