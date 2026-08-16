namespace Planning.Domain.Entities;

/// <summary>Singleton — paramètres de génération automatique des plannings.</summary>
public class PlanningAutoGenerateSettings
{
    public const string SingletonId = "default";

    public string Id { get; set; } = SingletonId;
    public bool Enabled { get; set; } = true;
    /// <summary>0=Sunday … 4=Thursday … 6=Saturday</summary>
    public int DayOfWeek { get; set; } = (int)System.DayOfWeek.Thursday;
    public int HourLocal { get; set; } = 6;
    public int MinuteLocal { get; set; } = 0;
    public string TimeZone { get; set; } = "Africa/Casablanca";
    /// <summary>NextWeek | CurrentWeek</summary>
    public string Target { get; set; } = "NextWeek";
    public DateTime? LastRunAt { get; set; }
    public string? LastRunWeekCode { get; set; }
    /// <summary>Date locale du dernier rappel J-1 demandes pending (anti-doublon).</summary>
    public DateOnly? LastPendingJ1ReminderDate { get; set; }
    /// <summary>WeekCode du dernier rappel RH phase validation.</summary>
    public string? LastValidationReminderWeekCode { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
}
