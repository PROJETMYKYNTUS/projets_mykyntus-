using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Planning.Application.Abstractions;
using Planning.Domain.Entities;
using Planning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Planning.Infrastructure.Services;

/// <summary>
/// Génère automatiquement les plannings brouillon selon PlanningAutoGenerateSettings
/// (défaut : jeudi → semaine suivante, Africa/Casablanca).
/// </summary>
public sealed class WeeklyPlanningAutoGeneratorHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<WeeklyPlanningAutoGeneratorHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("WeeklyPlanningAutoGenerator démarré.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Erreur job auto-génération plannings.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var planning = scope.ServiceProvider.GetRequiredService<IPlanningService>();

        var settings = await db.PlanningAutoGenerateSettings
            .FirstOrDefaultAsync(s => s.Id == PlanningAutoGenerateSettings.SingletonId, ct);

        if (settings is null || !settings.Enabled)
            return;

        DateTime localNow;
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(NormalizeTimeZone(settings.TimeZone));
            localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        }
        catch
        {
            localNow = DateTime.UtcNow;
        }

        if ((int)localNow.DayOfWeek != settings.DayOfWeek)
            return;

        if (localNow.Hour != settings.HourLocal || localNow.Minute != settings.MinuteLocal)
            return;

        // Évite double run dans la même minute / même semaine cible
        var targetMonday = GetTargetMonday(settings.Target, DateOnly.FromDateTime(localNow));
        var weekCode = FormatWeekCode(targetMonday);

        if (settings.LastRunWeekCode == weekCode
            && settings.LastRunAt.HasValue
            && settings.LastRunAt.Value.Date == DateTime.UtcNow.Date)
            return;

        logger.LogInformation("Auto-génération plannings pour {WeekCode}…", weekCode);
        var result = await planning.AutoGenerateWeekAsync(weekCode, forceDraftRefresh: false);
        logger.LogInformation(
            "Auto-génération {WeekCode}: created={Created} skipped={Skipped} errors={Errors}",
            result.WeekCode, result.Created, result.Skipped, result.Errors);
    }

    private static string NormalizeTimeZone(string tz)
    {
        // Windows vs Linux IANA
        if (string.Equals(tz, "Africa/Casablanca", StringComparison.OrdinalIgnoreCase)
            && OperatingSystem.IsWindows())
            return "Morocco Standard Time";
        return tz;
    }

    private static DateOnly GetTargetMonday(string target, DateOnly localToday)
    {
        var diff = ((int)localToday.DayOfWeek + 6) % 7;
        var currentMonday = localToday.AddDays(-diff);
        return string.Equals(target, "CurrentWeek", StringComparison.OrdinalIgnoreCase)
            ? currentMonday
            : currentMonday.AddDays(7);
    }

    private static string FormatWeekCode(DateOnly monday)
    {
        var dt = monday.ToDateTime(TimeOnly.MinValue);
        var week = System.Globalization.ISOWeek.GetWeekOfYear(dt);
        var year = System.Globalization.ISOWeek.GetYear(dt);
        return $"{year}-W{week:D2}";
    }
}
