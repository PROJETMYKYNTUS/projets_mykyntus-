using EmployeeDirectory.Application.Abstractions;
using EmployeeDirectory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EmployeeDirectory.Infrastructure.Persistence;

public static class DirectoryDatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
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

        await RunBestEffortAsync(log, "bootstrap pilotes projetés", () =>
            scope.ServiceProvider.GetRequiredService<IPilotRotationTenureService>()
                .BootstrapProjectedPilotsAsync(ct));
        await RunBestEffortAsync(log, "IAM permissions", () => SeedIamPermissionsAsync(db, ct));
        await RunBestEffortAsync(log, "demo département OP-001", () =>
            DockerComposeDirectoryDemoSeed.ApplyIfEnabledAsync(configuration, db, ct));
        await RunBestEffortAsync(log, "seed pilotage performance", () =>
            DockerComposePilotagePerformanceSeed.ApplyIfEnabledAsync(configuration, db, log, ct));

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

    private static async Task RunBestEffortAsync(ILogger log, string step, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Employee Directory : étape « {Step} » en échec (service démarre quand même).", step);
        }
    }

    private static async Task SeedIamPermissionsAsync(DirectoryDbContext db, CancellationToken ct)
    {
        if (await db.IamPermissions.AnyAsync(ct)) return;

        var seeds = new List<IamPermission>();
        void Add(string role, string action, string scope, bool allowed = true) =>
            seeds.Add(new IamPermission { Id = Guid.NewGuid(), Role = role, Action = action, Scope = scope, IsAllowed = allowed });

        foreach (var role in new[] { "Admin", "RH", "Audit" })
            Add(role, "*", "Global");

        foreach (var role in new[] { "Chef de projet", "RP" })
        {
            Add(role, "prime.fiche.view", "Pole");
            Add(role, "prime.fiche.validate", "Pole");
            Add(role, "planning.view", "Pole");
        }

        foreach (var role in new[] { "Superviseur", "Manager" })
        {
            Add(role, "prime.fiche.view", "Cellule");
            Add(role, "prime.fiche.validate", "Cellule");
            Add(role, "conge.approve", "Cellule");
            Add(role, "planning.edit", "Cellule");
        }

        foreach (var role in new[] { "Référent technique", "Coach" })
        {
            Add(role, "prime.fiche.view", "Service");
            Add(role, "formation.assign", "Service");
        }

        Add("Pilote", "conge.request", "Self");
        Add("Pilote", "prime.fiche.view", "Self");
        Add("Pilote", "documentation.request", "Self");
        Add("Pilote", "parrainage.view", "Self");

        db.IamPermissions.AddRange(seeds);
        await db.SaveChangesAsync(ct);
    }
}
