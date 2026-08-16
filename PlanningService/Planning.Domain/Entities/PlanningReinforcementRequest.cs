using System.ComponentModel.DataAnnotations;
using Planning.Domain.Enums;

namespace Planning.Domain.Entities;

/// <summary>
/// Demande de renfort samedi : superviseur → volontaires OFF → sélection.
/// N'impacte pas SaturdayHistory / rotation.
/// </summary>
public class PlanningReinforcementRequest
{
    public int Id { get; set; }

    [MaxLength(16)]
    public string WeekCode { get; set; } = string.Empty;

    public DateOnly SaturdayDate { get; set; }

    public int SubServiceId { get; set; }
    public SubService SubService { get; set; } = null!;

    public int SlotsNeeded { get; set; } = 1;

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    public PlanningReinforcementRequestStatus Status { get; set; }
        = PlanningReinforcementRequestStatus.Open;

    public int CreatedByUserId { get; set; }
    public User CreatedBy { get; set; } = null!;

    /// <summary>Superviseur ayant sélectionné le(s) volontaire(s).</summary>
    public int? SelectedByUserId { get; set; }

    /// <summary>Acteur ayant clôturé la demande (sélection ou annulation).</summary>
    public int? ClosedByUserId { get; set; }

    /// <summary>Superviseur ayant annulé l'appel au renfort.</summary>
    public int? CancelledByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    public int? WeeklyPlanningId { get; set; }
    public WeeklyPlanning? WeeklyPlanning { get; set; }

    public ICollection<PlanningReinforcementVolunteer> Volunteers { get; set; }
        = new List<PlanningReinforcementVolunteer>();
}
