using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Planning.Application.Abstractions;
using Planning.Domain.Entities;
using Planning.Infrastructure.Persistence;

namespace Planning.Infrastructure.Services;

/// <summary>
/// Rappels demandes non traitées : J-1 du jour de génération auto, à 09:00 locale.
/// </summary>
public sealed class PendingRequestsReminderHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<PendingRequestsReminderHostedService> logger) : BackgroundService
{
    private const int ReminderHourLocal = 9;
    private const int ReminderMinuteLocal = 0;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PendingRequestsReminder démarré.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Erreur job rappel demandes pending.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var alerts = scope.ServiceProvider.GetRequiredService<IPlanningPendingRequestsAlertService>();

        var settings = await db.PlanningAutoGenerateSettings
            .AsNoTracking()
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

        var reminderDay = (settings.DayOfWeek - 1 + 7) % 7;
        if ((int)localNow.DayOfWeek != reminderDay)
            return;

        if (localNow.Hour != ReminderHourLocal || localNow.Minute != ReminderMinuteLocal)
            return;

        var localDate = DateOnly.FromDateTime(localNow);
        var sent = await alerts.SendJ1RemindersAsync(localDate);
        if (sent)
            logger.LogInformation("Rappel J-1 demandes pending envoyé pour {Date}.", localDate);
    }

    private static string NormalizeTimeZone(string? tz)
    {
        if (string.IsNullOrWhiteSpace(tz)) return "Africa/Casablanca";
        // Windows alias
        if (tz.Equals("Africa/Casablanca", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(tz);
                return tz;
            }
            catch
            {
                return "Morocco Standard Time";
            }
        }
        return tz;
    }
}
