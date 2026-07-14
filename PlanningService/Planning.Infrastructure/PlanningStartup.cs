using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Planning.Application.Abstractions;
using Planning.Application.Abstractions.EmployeeImport;
using Planning.Domain.Entities;
using Planning.Infrastructure.Persistence;

namespace Planning.Infrastructure;

public static class PlanningStartup
{
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
            var importConfig = scope.ServiceProvider.GetRequiredService<IEmployeeImportConfigService>();
            await importConfig.EnsureSeedAsync();
        }

        using (var scope = services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await PlanningRoleSeed.EnsureManagerRoleAsync(context);
        }

        using (var scope = services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!context.Shifts.Any())
            {
                context.Shifts.AddRange(
                    new Shift { Label = "8h", StartTime = new TimeOnly(8, 0), LunchBreakTime = new TimeOnly(12, 0) },
                    new Shift { Label = "9h", StartTime = new TimeOnly(9, 0), LunchBreakTime = new TimeOnly(13, 0) },
                    new Shift { Label = "10h", StartTime = new TimeOnly(10, 0), LunchBreakTime = new TimeOnly(14, 0) },
                    new Shift { Label = "11h", StartTime = new TimeOnly(11, 0), LunchBreakTime = new TimeOnly(15, 0) });
                await context.SaveChangesAsync();
            }
        }

        using (var scope = services.CreateScope())
        {
            var planningService = scope.ServiceProvider.GetRequiredService<IPlanningService>();
            await planningService.SyncNewEmployeesAsync();
        }

        using (var scope = services.CreateScope())
        {
            var planningDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            try
            {
                await DockerComposePlanningDemoSeed.ApplyIfEnabledAsync(configuration, planningDb);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Demo seed planning ignoré: {ex.Message}");
            }
        }

        using (var scope = services.CreateScope())
        {
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            await userService.SyncMissingAuthUsersAsync();
            if (string.Equals(configuration["KYNTUS_PLANNING_DEMO_SEED"], "true", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await userService.SyncAllEmployesToCongeAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Sync congés ignorée: {ex.Message}");
                }
            }
        }

        try
        {
            await DockerComposePlanningEnrichmentSeed.ApplyIfEnabledAsync(services, configuration);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Planning enrichment ignoré: {ex.Message}");
        }

        using (var scope = services.CreateScope())
        {
            if (configuration.GetValue("Directory:EnablePlanningBootstrap", false))
            {
                try
                {
                    await PlanningDirectoryBootstrap.SyncExistingUsersToDirectoryAsync(scope.ServiceProvider);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Planning → Directory bootstrap ignoré: {ex.Message}");
                }
            }
        }

        using (var scope = services.CreateScope())
        {
            var newsletterService = scope.ServiceProvider.GetRequiredService<INewsletterService>();
            await newsletterService.RepairCampaignAnalyticsUserIdsAsync();
        }
    }

    public static void RegisterDirectoryOrgBootstrap(IServiceProvider services)
    {
        var hostLifetime = services.GetRequiredService<IHostApplicationLifetime>();
        hostLifetime.ApplicationStarted.Register(() =>
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                await PlanningDirectoryOrgBootstrap.SyncFromDirectoryAsync(services);
            });
        });
    }
}
