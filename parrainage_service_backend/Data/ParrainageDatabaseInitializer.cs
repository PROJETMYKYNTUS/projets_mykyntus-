using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParrainageBackend.Services;

namespace ParrainageBackend.Data;

/// <summary>
/// Applique les migrations EF au démarrage (avec rattrapage si le schéma a été créé par l'ancien EnsureCreated),
/// puis seed optionnel si <c>Parrainage:SeedDemoData</c> est activé.
/// </summary>
public sealed class ParrainageDatabaseInitializer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ParrainageDatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ParrainageDbContext>();

        await ParrainageSchemaPatches.BaselineLegacySchemaAsync(db, logger, cancellationToken);

        try
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.DuplicateTable)
        {
            logger.LogWarning(
                ex,
                "PARRAINAGE : Migrate a échoué (table déjà présente) — nouvelle tentative après baseline.");
            await ParrainageSchemaPatches.BaselineLegacySchemaAsync(db, logger, cancellationToken);
            await db.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PARRAINAGE : échec Migrate.");
            throw;
        }

        var seedDemo = configuration.GetValue("Parrainage:SeedDemoData", false);
        logger.LogInformation("PARRAINAGE : SeedDemoData={SeedDemo}.", seedDemo);
        if (seedDemo)
        {
            try
            {
                await ParrainageSeeder.SeedAsync(db, logger, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PARRAINAGE : échec du seed de démonstration.");
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
