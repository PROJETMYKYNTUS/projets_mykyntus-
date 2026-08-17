using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Planning.Domain.Entities;

/// <summary>
/// En-tête de validation hebdomadaire des modes pour une cellule.
/// Une semaine commencée est en lecture seule.
/// </summary>
public class WeeklyCellShiftModePlan
{
    public int Id { get; set; }

    [ForeignKey(nameof(SubService))]
    public int SubServiceId { get; set; }
    public SubService SubService { get; set; } = null!;

    [Required]
    [MaxLength(16)]
    public string WeekCode { get; set; } = string.Empty;

    public DateOnly WeekStartDate { get; set; }

    public bool IsValidated { get; set; }

    public DateTime? ValidatedAt { get; set; }

    public int? ValidatedByUserId { get; set; }
    public User? ValidatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<WeeklyEmployeeShiftMode> EmployeeModes { get; set; } = new List<WeeklyEmployeeShiftMode>();
}
