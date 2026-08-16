using Planning.Domain.Enums;

namespace Planning.Domain.Entities;

public class PlanningReinforcementVolunteer
{
    public int Id { get; set; }

    public int RequestId { get; set; }
    public PlanningReinforcementRequest Request { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public PlanningReinforcementVolunteerStatus Status { get; set; }
        = PlanningReinforcementVolunteerStatus.Pending;

    public DateTime? RespondedAt { get; set; }
    public DateTime? SelectedAt { get; set; }

    /// <summary>Template shift choisi à la sélection (IsTemplate).</summary>
    public int? SelectedShiftConfigId { get; set; }
    public SubServiceShiftConfig? SelectedShiftConfig { get; set; }
}
