namespace Planning.Domain.Entities;

public class SubServiceShiftConfig
{
    public int Id { get; set; }

    // -- Contexte --------------------------------------
    public int SubServiceId { get; set; }
    public SubService SubService { get; set; } = null!;

    /// <summary>null = template permanent ; ISO week code = snapshot semaine.</summary>
    public string? WeekCode { get; set; }
    public DateOnly? WeekStartDate { get; set; }

    /// <summary>true = modèle éditable ; false = snapshot généré pour une semaine.</summary>
    public bool IsTemplate { get; set; }

    // -- Définition du shift ---------------------------
    public string Label { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public int WorkHours { get; set; } = 8;

    // -- Pause déjeuner --------------------------------
    public TimeOnly BreakRangeStart { get; set; }
    public TimeOnly BreakRangeEnd { get; set; }
    public int BreakDurationMinutes { get; set; } = 60;

    // -- Quota -----------------------------------------
    public int RequiredCount { get; set; }
    public decimal Percentage { get; set; }

    // -- Règle présence --------------------------------
    public int MinPresencePercent { get; set; } = 70;

    // -- Ordre affichage -------------------------------
    public int DisplayOrder { get; set; }

    // -- Audit -----------------------------------------
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public TimeOnly EndTime =>
        StartTime.AddHours(WorkHours + (BreakDurationMinutes / 60.0));
}
