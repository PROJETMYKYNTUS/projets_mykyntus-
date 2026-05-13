using Microsoft.EntityFrameworkCore;
using PrimeBackend.Services;

namespace PrimeBackend.Data;

public sealed class PrimeDatabaseInitializer(
    IServiceScopeFactory scopeFactory,
    ILogger<PrimeDatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
            if (!await db.Departments.AnyAsync(cancellationToken))
                await PrimeDbSeeder.SeedAsync(db, cancellationToken);

            var store = scope.ServiceProvider.GetRequiredService<PrimeInMemoryStore>();
            store.HydrateOrganizationFromDatabase(db);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Prime database initialization skipped or failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
