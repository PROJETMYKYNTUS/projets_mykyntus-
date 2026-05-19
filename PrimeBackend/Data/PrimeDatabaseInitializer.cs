using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PrimeBackend.Services;

namespace PrimeBackend.Data;

public sealed class PrimeDatabaseInitializer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<PrimeDatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();

        try
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PRIME : échec MigrateAsync — tentative correctif schéma OrgOptional.");
        }

        try
        {
            await PrimeSchemaPatches.EnsureOrgOptionalAndDraftRootPoleAsync(db, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PRIME : correctif schéma OrgOptional non appliqué.");
            throw;
        }

        await EnsurePrimeMetierTablesExistAsync(db, cancellationToken);

        // Seed / enrichissement en arrière-plan : ne bloque pas Kestrel (évite 502 gateway pendant l’enrichissement).
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await RunSeedAndHydrateAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "PRIME : échec seed / enrichissement / hydrate (arrière-plan).");
                }
            },
            cancellationToken);
    }

    private async Task RunSeedAndHydrateAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();

        await PrimeDbSeeder.EnsureOperationalFicheWorkflowOnlyAsync(db, cancellationToken: cancellationToken);
        await PrimeDbSeeder.SeedMissingReferentTechnicalValidateRbacAsync(db, cancellationToken);
        if (!await db.Poles.AnyAsync(cancellationToken))
        {
            var seedDemo = configuration.GetValue("Prime:SeedDemoData", true);
            logger.LogInformation("PRIME : seed initial (SeedDemoData={SeedDemo})…", seedDemo);
            await PrimeDbSeeder.SeedAsync(db, seedDemo, cancellationToken);
        }

        var enrichDemo = configuration.GetValue("Prime:EnrichDemoData", environment.IsDevelopment());
        logger.LogInformation(
            "PRIME : EnrichDemoData={EnrichDemo} (env={Environment})",
            enrichDemo,
            environment.EnvironmentName);

        if (enrichDemo)
        {
            var markerApplied = await PrimeDbEnrichmentSeeder.IsVersionAppliedAsync(db, cancellationToken);
            var hasData = await PrimeDbEnrichmentSeeder.HasEnrichmentDataAsync(db, cancellationToken);
            var forceRepair = markerApplied && !hasData;
            if (forceRepair)
                logger.LogWarning("PRIME : marqueur enrichissement présent mais données absentes — réparation automatique.");

            await PrimeDbEnrichmentSeeder.EnrichAsync(db, forceRepair, cancellationToken, logger);
        }

        var store = scope.ServiceProvider.GetRequiredService<PrimeInMemoryStore>();
        store.HydrateOrganizationFromDatabase(db);
        logger.LogInformation("PRIME : base prête (hydrate store terminé).");
    }

    /// <summary>
    /// Après Migrate, vérifie que le schéma métier V2 est bien présent. Sinon les API renvoient 42P01 peu lisibles.
    /// </summary>
    private static async Task EnsurePrimeMetierTablesExistAsync(PrimeDbContext db, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_name = 'prime_workflow_step');
                """;
            var scalar = await cmd.ExecuteScalarAsync(ct);
            var present = scalar switch
            {
                true => true,
                false => false,
                long l => l != 0,
                int i => i != 0,
                _ => false,
            };
            if (present)
                return;

            throw new InvalidOperationException(
                "Schéma PRIME incomplet : la table « prime_workflow_step » est absente dans prime_db. "
                + "Les migrations EF n’ont pas été appliquées (ou le volume PostgreSQL est incohérent). "
                + "Corrigez en appliquant les migrations : depuis le dossier PrimeBackend, "
                + "`dotnet ef database update` avec la chaîne vers prime_db ; "
                + "en dev Docker, vous pouvez recréer le volume : `docker compose down -v` puis `docker compose up -d` (données locales perdues).");
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
