namespace PlanningService.Models;

public class SubServiceShiftConfig
{
    public int Id { get; set; }

    // ── Contexte ──────────────────────────────────────
    public int SubServiceId { get; set; }
    public SubService SubService { get; set; } = null!;

    public string WeekCode { get; set; } = string.Empty;  // "2026-W10"
    public DateOnly WeekStartDate { get; set; }

    // ── Définition du shift ───────────────────────────
    public string Label { get; set; } = string.Empty;     // "Matin", "Tardif"...
    public TimeOnly StartTime { get; set; }               // 08:00
    // EndTime = StartTime + WorkHours + 1h pause (calculé)
    public int WorkHours { get; set; } = 8;               // 8h par défaut, modifiable

    // ── Pause déjeuner ────────────────────────────────
    // Plage dans laquelle l'algo peut placer la pause
    public TimeOnly BreakRangeStart { get; set; }         // ex: 11:00 (début plage possible)
    public TimeOnly BreakRangeEnd { get; set; }           // ex: 14:00 (fin plage possible)
    // Durée fixe 1h, mais modifiable si exception sous-service
    public int BreakDurationMinutes { get; set; } = 60;

    // ── Quota ─────────────────────────────────────────
    public int RequiredCount { get; set; }                // nb employés sur ce shift
    public decimal Percentage { get; set; }               // calculé auto

    // ── Règle présence ────────────────────────────────
    public int MinPresencePercent { get; set; } = 70;     // 70% présents minimum
    // => max 30% en pause simultanément

    // ── Ordre affichage ───────────────────────────────
    public int DisplayOrder { get; set; }                 // 1, 2, 3, 4...

    // ── Audit ─────────────────────────────────────────
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ── Propriété calculée (pas en base) ─────────────
    public TimeOnly EndTime =>
        StartTime.AddHours(WorkHours + (BreakDurationMinutes / 60));
}