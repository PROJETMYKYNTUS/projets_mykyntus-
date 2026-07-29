using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Parrainage.Infrastructure.Services;

namespace Parrainage.Infrastructure.Persistence;

/// <summary>
/// Applique les migrations EF au démarrage (avec rattrapage si le schéma a été créé par l'ancien EnsureCreated).
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

        await ParrainageSchemaPatches.ApplyPendingSchemaAsync(db, logger, cancellationToken);

        var seedDemo = configuration.GetValue("Parrainage:SeedDemoData", false)
            && string.Equals(configuration["KYNTUS_DEMO_ENRICHMENT"] ?? "false", "true", StringComparison.OrdinalIgnoreCase);
        if (seedDemo)
        {
            try
            {
                await ParrainageSeeder.SeedAsync(db, logger, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "PARRAINAGE : seed démo ignoré.");
            }
        }
        else
        {
            logger.LogInformation("PARRAINAGE : base prête (sans seed démo).");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
