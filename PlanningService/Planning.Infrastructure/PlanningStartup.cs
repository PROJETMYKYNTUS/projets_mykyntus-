using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Planning.Application.Abstractions;
using Planning.Infrastructure.Persistence;

namespace Planning.Infrastructure;

public static class PlanningStartup
{
    /// <summary>
    /// Schéma + seed uniquement — doit rester rapide pour que /health réponde
    /// avant le timeout Compose (sinon gateway/frontends restent en Created).
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var maxRetries = 10;
            for (var i = 0; i < maxRetries; i++)
            {
                try
                {
                    await db.Database.MigrateAsync();
                    await PlanningSchemaPatches.EnsureOutboxTableAsync(db);
                    await PlanningSchemaPatches.EnsurePlanningNotificationsTableAsync(db);
                    await PlanningSchemaPatches.EnsureUserHrProfilesTableAsync(db);
                    await PlanningSchemaPatches.EnsureDateDebutFormationColumnAsync(db);
                    await PlanningSchemaPatches.EnsureNumeroCarteAutoentrepreneurColumnAsync(db);
                    await PlanningSchemaPatches.EnsureEmailPersonnelColumnAsync(db);
                    await PlanningSchemaPatches.EnsureCongeSourceDemandeIdColumnAsync(db);
                    await PlanningSchemaPatches.EnsureShiftTemplateAndValidationSchemaAsync(db);
                    await PlanningSchemaPatches.EnsureShiftKindColumnAsync(db);
                    await PlanningSchemaPatches.EnsureBreakSlotsAndCriticalCellAsync(db);
                    await PlanningSchemaPatches.EnsurePlanningChangeRequestsTableAsync(db);
                    await PlanningSchemaPatches.EnsurePlanningExceptionalRequestsTableAsync(db);
                    await PlanningSchemaPatches.EnsureShiftAssignmentExceptionalFlagAsync(db);
                    await PlanningSchemaPatches.EnsurePlanningReinforcementRequestsTableAsync(db);
                    await PlanningSchemaPatches.EnsurePendingRequestReminderColumnsAsync(db);
                    await PlanningSchemaPatches.EnsureEmployeeImportSourceFileColumnsAsync(db);
                    await PlanningSchemaPatches.EnsureUserHtelColumnsAsync(db);
                    await PlanningSchemaPatches.EnsureUserSaturdayWorkModeColumnAsync(db);
                    await PlanningSchemaPatches.EnsureUserSpecialCaseColumnsAsync(db);
                    await PlanningSchemaPatches.EnsureUserPlateauTrainingColumnAsync(db);
                    await PlanningSchemaPatches.EnsureShiftModeProfilesSchemaAsync(db);
                    await PlanningSchemaPatches.EnsureMediaAndTicketCommentsAsync(db);
                    Console.WriteLine("✅ Migrations appliquées avec succès.");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⏳ Attente DB... tentative {i + 1}/{maxRetries}: {ex.Message}");
                    Thread.Sleep(3000);
                }
            }
        }

        using (var scope = services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await PlanningRoleSeed.EnsureCatalogAsync(context);
        }
    }

    /// <summary>
    /// Syncs lourdes (Auth, employés, newsletter, Directory) après écoute HTTP
    /// pour ne pas bloquer le healthcheck Docker Compose.
    /// </summary>
    public static void RegisterPostListenBootstrap(IServiceProvider services, IConfiguration configuration)
    {
        var hostLifetime = services.GetRequiredService<IHostApplicationLifetime>();
        hostLifetime.ApplicationStarted.Register(() =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using (var scope = services.CreateScope())
                    {
                        var planningService = scope.ServiceProvider.GetRequiredService<IPlanningService>();
                        await planningService.SyncNewEmployeesAsync();
                    }

                    using (var scope = services.CreateScope())
                    {
                        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
                        await userService.SyncMissingAuthUsersAsync();
                    }

                    if (configuration.GetValue("Directory:EnablePlanningBootstrap", false))
                    {
                        using var scope = services.CreateScope();
                        try
                        {
                            await PlanningDirectoryBootstrap.SyncExistingUsersToDirectoryAsync(scope.ServiceProvider);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️ Planning → Directory bootstrap ignoré: {ex.Message}");
                        }
                    }

                    using (var scope = services.CreateScope())
                    {
                        var newsletterService = scope.ServiceProvider.GetRequiredService<INewsletterService>();
                        await newsletterService.RepairCampaignAnalyticsUserIdsAsync();
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5));
                    await PlanningDirectoryOrgBootstrap.SyncFromDirectoryAsync(services);

                    try
                    {
                        await DockerComposePlanningEnrichmentSeed.ApplyIfEnabledAsync(services, configuration);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Planning enrichment ignoré: {ex.Message}");
                    }

                    try
                    {
                        await DockerComposeFormationNotificationsSeed.ApplyIfEnabledAsync(services, configuration);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Notifications formation seed ignoré: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Bootstrap post-écoute ignoré: {ex.Message}");
                }
            });
        });
    }
}
