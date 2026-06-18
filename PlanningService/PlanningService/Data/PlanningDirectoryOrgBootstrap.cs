using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlanningService.Services;

namespace PlanningService.Data;

/// <summary>Aligne le miroir org Planning depuis Employee Directory au démarrage.</summary>
internal static class PlanningDirectoryOrgBootstrap
{
    internal static async Task SyncFromDirectoryAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var mirror = scope.ServiceProvider.GetRequiredService<IPlanningOrgMirrorService>();
        var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("PlanningDirectoryOrgBootstrap");

        try
        {
            var actions = await mirror.SyncFromDirectoryOverviewAsync(authorizationHeader: null, ct);
            log.LogInformation("Planning org mirror (Directory) : {Count} action(s).", actions);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Planning org mirror (Directory) ignoré au démarrage.");
        }
    }
}
