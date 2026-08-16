namespace Planning.Application.DTOs.Planning;

public class CreatePlanningReinforcementRequestDto
{
    public int SubServiceId { get; set; }
    public DateOnly SaturdayDate { get; set; }
    public int SlotsNeeded { get; set; } = 1;
    public string Reason { get; set; } = string.Empty;
}

public class SelectReinforcementVolunteersDto
{
    public List<int> UserIds { get; set; } = new();
    public int ShiftConfigId { get; set; }
}

public class PlanningReinforcementVolunteerDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? RespondedAt { get; set; }
    public DateTime? SelectedAt { get; set; }
    public int? SelectedShiftConfigId { get; set; }
    public string? SelectedShiftLabel { get; set; }

    /// <summary>Heures programmées (semaine du samedi ciblé).</summary>
    public decimal ScheduledHoursWeek { get; set; }

    /// <summary>Heures programmées (mois calendaire courant).</summary>
    public decimal ScheduledHoursMonth { get; set; }
}

public class PlanningReinforcementRequestDto
{
    public int Id { get; set; }
    public string WeekCode { get; set; } = string.Empty;
    public DateOnly SaturdayDate { get; set; }
    public int SubServiceId { get; set; }
    public string SubServiceName { get; set; } = string.Empty;
    public int SlotsNeeded { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public int? SelectedByUserId { get; set; }
    public int? ClosedByUserId { get; set; }
    public int? CancelledByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int? WeeklyPlanningId { get; set; }
    public int AcceptedCount { get; set; }
    public int SelectedCount { get; set; }
    public int EligibleCount { get; set; }

    /// <summary>Statut du volontaire courant (vue pilote), si applicable.</summary>
    public string? MyVolunteerStatus { get; set; }

    public List<PlanningReinforcementVolunteerDto> Volunteers { get; set; } = new();
}

public class ReinforcementContributorStatsDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int? SubServiceId { get; set; }
    public string SubServiceName { get; set; } = string.Empty;
    public int Solicited { get; set; }
    public int Accepted { get; set; }
    public int Selected { get; set; }
    public int Declined { get; set; }
    public decimal AcceptanceRate { get; set; }
}
