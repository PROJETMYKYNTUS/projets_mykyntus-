using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EmployeeDirectory.Infrastructure.Persistence;

public static class DirectoryDatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DirectoryInit");

        try
        {
            await EnsureDatabaseReadyAsync(db, log, ct);
        }
        catch (Exception ex)
        {
            // Ne jamais bloquer le listen HTTP : sinon Compose marque le service unhealthy
            // et bloque planning / gateway. Les endpoints métier échoueront jusqu'à réparation DB.
            log.LogCritical(ex,
                "Employee Directory : base indisponible au démarrage — l'API démarre quand même (health OK).");
            return;
        }

        log.LogInformation("Employee Directory database ready.");
    }

    private static async Task EnsureDatabaseReadyAsync(
        DirectoryDbContext db,
        ILogger log,
        CancellationToken ct)
    {
        const int maxRetries = 20;
        Exception? last = null;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await db.Database.EnsureCreatedAsync(ct);
                await DirectorySchemaPatches.ApplyAsync(db, ct);
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                last = ex;
                log.LogWarning(ex, "Waiting for directory DB... attempt {Attempt}/{MaxRetries}", attempt, maxRetries);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw new InvalidOperationException(
            "Employee Directory database initialization failed after retries.", last);
    }
}
