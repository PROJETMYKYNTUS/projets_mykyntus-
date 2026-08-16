using Conge.Domain.Entities;
using Conge.Domain.Interfaces;
using Conge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Conge.Infrastructure.Persistence;

/// <summary>
/// Patches idempotents (colonnes / tables) + seed période interdite défaut mois 9–10.
/// Complète les migrations EF pour les déploiements existants.
/// </summary>
public static class CongeSchemaPatches
{
    public static async Task ApplyAsync(CongeDbContext db, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE demandes_conge ADD COLUMN IF NOT EXISTS "CommentaireRh" character varying(500);
                ALTER TABLE demandes_conge ADD COLUMN IF NOT EXISTS "DateValidationSuperviseur" timestamp with time zone;
                ALTER TABLE demandes_conge ADD COLUMN IF NOT EXISTS "ValidationNodeId" character varying(100);
                ALTER TABLE demandes_conge ADD COLUMN IF NOT EXISTS "ValidationNodeLevel" character varying(50);
                ALTER TABLE demandes_conge ADD COLUMN IF NOT EXISTS "SuperviseurDecideurId" uuid;
                ALTER TABLE demandes_conge ADD COLUMN IF NOT EXISTS "RhDecideurId" uuid;
                """, ct);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE INDEX IF NOT EXISTS "IX_demandes_conge_ValidationNodeId"
                    ON demandes_conge ("ValidationNodeId");
                """, ct);

            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE employe_snapshots ADD COLUMN IF NOT EXISTS "PoleId" character varying(100);
                ALTER TABLE employe_snapshots ADD COLUMN IF NOT EXISTS "CelluleId" character varying(100);
                ALTER TABLE employe_snapshots ADD COLUMN IF NOT EXISTS "OrgServiceId" character varying(100);
                ALTER TABLE employe_snapshots ADD COLUMN IF NOT EXISTS "BusinessDepartmentId" uuid;
                """, ct);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE INDEX IF NOT EXISTS "IX_employe_snapshots_CelluleId"
                    ON employe_snapshots ("CelluleId");
                CREATE INDEX IF NOT EXISTS "IX_employe_snapshots_OrgServiceId"
                    ON employe_snapshots ("OrgServiceId");
                CREATE INDEX IF NOT EXISTS "IX_employe_snapshots_PoleId"
                    ON employe_snapshots ("PoleId");
                """, ct);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS demande_conge_decisions (
                    "Id" uuid NOT NULL PRIMARY KEY,
                    "DemandeId" uuid NOT NULL,
                    "ActeurId" uuid NOT NULL,
                    "ActeurNom" character varying(200) NOT NULL,
                    "ActeurRole" character varying(100) NOT NULL,
                    "Action" character varying(50) NOT NULL,
                    "StatutAvant" character varying(50) NOT NULL,
                    "StatutApres" character varying(50) NOT NULL,
                    "Commentaire" character varying(500) NULL,
                    "At" timestamp with time zone NOT NULL,
                    CONSTRAINT "FK_demande_conge_decisions_DemandeId"
                        FOREIGN KEY ("DemandeId") REFERENCES demandes_conge ("Id") ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS "IX_demande_conge_decisions_DemandeId"
                    ON demande_conge_decisions ("DemandeId");
                CREATE INDEX IF NOT EXISTS "IX_demande_conge_decisions_ActeurId"
                    ON demande_conge_decisions ("ActeurId");
                CREATE INDEX IF NOT EXISTS "IX_demande_conge_decisions_At"
                    ON demande_conge_decisions ("At");
                """, ct);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS periodes_interdites_conge (
                    "Id" uuid NOT NULL PRIMARY KEY,
                    "MoisInterditsJson" character varying(200) NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "UpdatedBy" uuid NULL
                );
                """, ct);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS quotas_conge_service (
                    "Id" uuid NOT NULL PRIMARY KEY,
                    "ServiceId" uuid NOT NULL,
                    "MaxAbsentsSimultanes" integer NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "UpdatedBy" uuid NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_quotas_conge_service_ServiceId"
                    ON quotas_conge_service ("ServiceId");
                """, ct);

            if (!await db.PeriodesInterdites.AnyAsync(ct))
            {
                db.PeriodesInterdites.Add(PeriodeInterditeConge.CreerParDefaut());
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Seed période interdite congés (mois 9, 10).");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CongeSchemaPatches : patch partiellement ignoré (SQLite tests ou DB non-Postgres).");
            await EnsureSeedForSqliteAsync(db, ct);
        }
    }

    private static async Task EnsureSeedForSqliteAsync(CongeDbContext db, CancellationToken ct)
    {
        try
        {
            await db.Database.EnsureCreatedAsync(ct);
            if (!await db.PeriodesInterdites.AnyAsync(ct))
            {
                db.PeriodesInterdites.Add(PeriodeInterditeConge.CreerParDefaut());
                await db.SaveChangesAsync(ct);
            }
        }
        catch
        {
            /* ignore */
        }
    }
}
