using Microsoft.EntityFrameworkCore;

namespace EmployeeDirectory.Infrastructure.Data;

public static class DirectorySchemaPatches
{
    public static async Task ApplyAsync(DirectoryDbContext db, CancellationToken ct = default)
    {
        await EnsureOutboxTableAsync(db, ct);
        await EnsureEmployeeRowVersionDefaultAsync(db, ct);
    }

    public static async Task EnsureOutboxTableAsync(DirectoryDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS outbox_messages (
                "Id" uuid NOT NULL PRIMARY KEY,
                "MessageType" character varying(512) NOT NULL,
                "PayloadJson" text NOT NULL,
                "CorrelationId" text NULL,
                "AggregateId" text NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "ProcessedAt" timestamp with time zone NULL,
                "Error" text NULL,
                "RetryCount" integer NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS "IX_outbox_messages_ProcessedAt" ON outbox_messages ("ProcessedAt");
            """,
            ct);
    }

    /// <summary>
    /// PostgreSQL n'a pas de rowversion auto : la colonne doit avoir une valeur à l'INSERT.
    /// </summary>
    public static async Task EnsureEmployeeRowVersionDefaultAsync(DirectoryDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE employees
                ALTER COLUMN "RowVersion" SET DEFAULT '\x00'::bytea;
            UPDATE employees
                SET "RowVersion" = '\x00'::bytea
                WHERE "RowVersion" IS NULL;
            """,
            ct);
    }
}
