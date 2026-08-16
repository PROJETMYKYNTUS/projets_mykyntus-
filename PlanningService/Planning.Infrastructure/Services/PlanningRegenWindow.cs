namespace Planning.Infrastructure.Services;

/// <summary>
/// Fenêtre de régénération : pas de regen sur le jour J ;
/// après 15h, demain est aussi figé (shifts ~16h).
/// </summary>
public static class PlanningRegenWindow
{
    public const int CutoffHour = 15;

    /// <summary>
    /// Premier jour encore régénérable (inclus), selon l'heure locale serveur.
    /// </summary>
    public static DateOnly GetEarliestRegenerableDate(DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        return now.TimeOfDay >= TimeSpan.FromHours(CutoffHour)
            ? today.AddDays(2)
            : today.AddDays(1);
    }

    /// <summary>
    /// true si la date est encore dans la fenêtre de regen.
    /// </summary>
    public static bool IsRegenerable(DateOnly date, DateTime now) =>
        date >= GetEarliestRegenerableDate(now);
}
