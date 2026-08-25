using System.Globalization;
using Planning.Domain.Entities;

namespace Planning.Infrastructure.Services;

/// <summary>
/// Deadline modes superviseur = veille du jour d’auto-génération RH, 23:59 (TZ settings).
/// Ex. génération mardi → deadline lundi 23:59.
/// </summary>
public static class ShiftModePlanDeadline
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public static DateTime ComputeCarryDeadlineLocal(
        PlanningAutoGenerateSettings settings,
        DateOnly targetWeekMonday)
    {
        var prevMonday = targetWeekMonday.AddDays(-7);
        var daysFromMonday = ((settings.DayOfWeek % 7) + 6) % 7;
        var generationDate = prevMonday.AddDays(daysFromMonday);
        return generationDate.AddDays(-1).ToDateTime(new TimeOnly(23, 59, 59));
    }

    public static DateOnly ComputeGenerationDate(PlanningAutoGenerateSettings settings, DateOnly targetWeekMonday)
    {
        var prevMonday = targetWeekMonday.AddDays(-7);
        var daysFromMonday = ((settings.DayOfWeek % 7) + 6) % 7;
        return prevMonday.AddDays(daysFromMonday);
    }

    public static DateTime ToLocalNow(PlanningAutoGenerateSettings settings, DateTime utcNow)
    {
        var tz = ResolveTimeZone(settings.TimeZone);
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), tz);
    }

    /// <summary>
    /// Semaine déjà commencée (ou passée) : pas de gate superviseur.
    /// Semaine future : bloquée tant que now ≤ deadline et aucun plan validé.
    /// </summary>
    public static bool ShouldBlockUntilSupervisorSave(
        PlanningAutoGenerateSettings settings,
        DateOnly targetWeekMonday,
        DateOnly todayLocal,
        DateTime nowLocal)
    {
        if (targetWeekMonday <= todayLocal)
            return false;
        var deadline = ComputeCarryDeadlineLocal(settings, targetWeekMonday);
        return nowLocal <= deadline;
    }

    public static string FormatPendingMessage(
        PlanningAutoGenerateSettings settings,
        DateOnly targetWeekMonday)
    {
        var deadline = ComputeCarryDeadlineLocal(settings, targetWeekMonday);
        var genDate = ComputeGenerationDate(settings, targetWeekMonday);
        var deadlineLabel = deadline.ToString("dddd dd/MM/yyyy HH:mm", Fr);
        var genLabel = genDate.ToDateTime(TimeOnly.MinValue).ToString("dddd", Fr);
        return
            $"Le superviseur n’a pas encore configuré les modes " +
            $"(deadline {deadlineLabel}, génération auto {genLabel}).";
    }

    public static TimeZoneInfo ResolveTimeZone(string? tzId)
    {
        var id = string.IsNullOrWhiteSpace(tzId) ? "Africa/Casablanca" : tzId;
        try
        {
            if (string.Equals(id, "Africa/Casablanca", StringComparison.OrdinalIgnoreCase)
                && OperatingSystem.IsWindows())
                return TimeZoneInfo.FindSystemTimeZoneById("Morocco Standard Time");
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Morocco Standard Time"); }
            catch { return TimeZoneInfo.Utc; }
        }
    }
}
