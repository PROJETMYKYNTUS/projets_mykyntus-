using Microsoft.EntityFrameworkCore;

namespace PrimeBackend.Data;

/// <summary>
/// Correctifs idempotents si une migration EF n’a pas été appliquée (ex. fichier sans Designer).
/// </summary>
public static class PrimeSchemaPatches
{
    public static async Task EnsureOrgOptionalAndDraftRootPoleAsync(PrimeDbContext db, CancellationToken ct = default)
    {
        if (!await TableExistsAsync(db, "prime_employee", ct) ||
            !await TableExistsAsync(db, "prime_supervisor_cellule_prime_draft", ct))
            return;

        await db.Database.ExecuteSqlRawAsync(
            """
            DO $$
            BEGIN
              IF EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'prime_employee'
                  AND column_name = 'CelluleId' AND is_nullable = 'NO'
              ) THEN
                ALTER TABLE prime_employee ALTER COLUMN "CelluleId" DROP NOT NULL;
              END IF;
              IF EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'prime_employee'
                  AND column_name = 'ServiceId' AND is_nullable = 'NO'
              ) THEN
                ALTER TABLE prime_employee ALTER COLUMN "ServiceId" DROP NOT NULL;
              END IF;
            END $$;
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            DO $$
            BEGIN
              IF NOT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'prime_supervisor_cellule_prime_draft'
                  AND column_name = 'RootPoleId'
              ) THEN
                ALTER TABLE prime_supervisor_cellule_prime_draft
                  ADD COLUMN "RootPoleId" character varying(128);
              END IF;
            END $$;
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE prime_supervisor_cellule_prime_draft AS d
            SET "RootPoleId" = c."PoleId"
            FROM prime_cellule AS c
            WHERE (d."RootPoleId" IS NULL OR d."RootPoleId" = '')
              AND d."CelluleId" = c."Id";
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE prime_supervisor_cellule_prime_draft AS d
            SET "RootPoleId" = d."CelluleId"
            WHERE (d."RootPoleId" IS NULL OR d."RootPoleId" = '')
              AND EXISTS (SELECT 1 FROM prime_pole p WHERE p."Id" = d."CelluleId");
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM prime_supervisor_cellule_prime_draft d
            WHERE d."RootPoleId" IS NULL OR d."RootPoleId" = ''
               OR NOT EXISTS (SELECT 1 FROM prime_pole p WHERE p."Id" = d."RootPoleId");
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            DO $$
            BEGIN
              IF EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'prime_supervisor_cellule_prime_draft'
                  AND column_name = 'RootPoleId' AND is_nullable = 'YES'
              ) THEN
                ALTER TABLE prime_supervisor_cellule_prime_draft
                  ALTER COLUMN "RootPoleId" SET NOT NULL;
              END IF;
            END $$;
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            DROP INDEX IF EXISTS "IX_prime_supervisor_cellule_prime_draft_SupervisorUserId_CelluleId_Period_TemplateId";
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_prime_supervisor_cellule_prime_draft_SupervisorUserId_CelluleId_Period_TemplateId"
              ON prime_supervisor_cellule_prime_draft ("SupervisorUserId", "CelluleId", "Period", "TemplateId");
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM prime_supervisor_cellule_prime_draft a
            USING prime_supervisor_cellule_prime_draft b
            WHERE a.ctid < b.ctid
              AND a."SupervisorUserId" = b."SupervisorUserId"
              AND a."RootPoleId" = b."RootPoleId"
              AND a."Period" = b."Period";
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_prime_supervisor_cellule_prime_draft_SupervisorUserId_RootPoleId_Period"
              ON prime_supervisor_cellule_prime_draft ("SupervisorUserId", "RootPoleId", "Period");
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_prime_supervisor_cellule_prime_draft_RootPoleId"
              ON prime_supervisor_cellule_prime_draft ("RootPoleId");
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            DO $$
            BEGIN
              IF NOT EXISTS (
                SELECT 1 FROM pg_constraint
                WHERE conname = 'FK_prime_supervisor_cellule_prime_draft_prime_pole_RootPoleId'
              ) THEN
                ALTER TABLE prime_supervisor_cellule_prime_draft
                  ADD CONSTRAINT "FK_prime_supervisor_cellule_prime_draft_prime_pole_RootPoleId"
                  FOREIGN KEY ("RootPoleId") REFERENCES prime_pole ("Id") ON DELETE RESTRICT;
              END IF;
            END $$;
            """,
            ct);
    }

    private static Task<bool> TableExistsAsync(PrimeDbContext db, string tableName, CancellationToken ct) =>
        tableName switch
        {
            "prime_employee" => ScalarBoolAsync(
                db,
                """
                SELECT EXISTS (
                  SELECT 1 FROM information_schema.tables
                  WHERE table_schema = 'public' AND table_name = 'prime_employee');
                """,
                ct),
            "prime_supervisor_cellule_prime_draft" => ScalarBoolAsync(
                db,
                """
                SELECT EXISTS (
                  SELECT 1 FROM information_schema.tables
                  WHERE table_schema = 'public' AND table_name = 'prime_supervisor_cellule_prime_draft');
                """,
                ct),
            _ => Task.FromResult(false),
        };

    private static async Task<bool> ScalarBoolAsync(PrimeDbContext db, string sql, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = sql;
            var scalar = await cmd.ExecuteScalarAsync(ct);
            return scalar switch
            {
                true => true,
                false => false,
                long l => l != 0,
                int i => i != 0,
                _ => false,
            };
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
