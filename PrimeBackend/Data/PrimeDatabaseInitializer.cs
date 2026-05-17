using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PrimeBackend.Services;

namespace PrimeBackend.Data;

public sealed class PrimeDatabaseInitializer(IServiceScopeFactory scopeFactory, IConfiguration configuration) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        await EnsurePrimeMetierTablesExistAsync(db, cancellationToken);
        await PrimeDbSeeder.EnsureOperationalFicheWorkflowOnlyAsync(db, cancellationToken: cancellationToken);
        await PrimeDbSeeder.SeedMissingReferentTechnicalValidateRbacAsync(db, cancellationToken);
        if (!await db.Poles.AnyAsync(cancellationToken))
        {
            // PrimeDbSeeder.SeedAsync: set Prime:SeedDemoData=false in production for core-only seed (no demo fiches).
            var seedDemo = configuration.GetValue("Prime:SeedDemoData", true);
            await PrimeDbSeeder.SeedAsync(db, seedDemo, cancellationToken);
        }

        var store = scope.ServiceProvider.GetRequiredService<PrimeInMemoryStore>();
        store.HydrateOrganizationFromDatabase(db);
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
