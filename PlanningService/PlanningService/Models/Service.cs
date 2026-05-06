using PlanningService.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Service
{
    public int Id { get; set; }

    [ForeignKey("Floor")]
    public int FloorId { get; set; }
    public Floor Floor { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; }

    [Required]
    [MaxLength(20)]
    public string Code { get; set; }

    public ICollection<SubService> SubServices { get; set; }
}
