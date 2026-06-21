using Microsoft.EntityFrameworkCore;

namespace EmployeeDirectory.Infrastructure.Data;

public static class DirectorySchemaPatches
{
    public static async Task ApplyAsync(DirectoryDbContext db, CancellationToken ct = default)
    {
        await EnsureOutboxTableAsync(db, ct);
        await EnsureEmployeeRowVersionDefaultAsync(db, ct);
        await EnsureBusinessDepartmentSchemaAsync(db, ct);
        await EnsureOrgPoleBusinessDepartmentAsync(db, ct);
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

    public static async Task EnsureBusinessDepartmentSchemaAsync(DirectoryDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS business_departments (
                "Id" uuid NOT NULL PRIMARY KEY,
                "Code" character varying(64) NOT NULL,
                "Name" character varying(256) NOT NULL,
                "Kind" integer NOT NULL DEFAULT 0,
                "ManagerEmployeeId" uuid NULL,
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_business_departments_Code" ON business_departments ("Code");

            CREATE TABLE IF NOT EXISTS department_pole_assignments (
                "Id" uuid NOT NULL PRIMARY KEY,
                "BusinessDepartmentId" uuid NOT NULL REFERENCES business_departments("Id") ON DELETE CASCADE,
                "PoleId" character varying(64) NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_department_pole_assignments_Dept_Pole"
                ON department_pole_assignments ("BusinessDepartmentId", "PoleId");

            ALTER TABLE employees ADD COLUMN IF NOT EXISTS "BusinessDepartmentId" uuid NULL;
            CREATE INDEX IF NOT EXISTS "IX_employees_BusinessDepartmentId" ON employees ("BusinessDepartmentId");
            """,
            ct);
    }

    /// <summary>
    /// FK org_poles → business_departments ; backfill depuis department_pole_assignments
    /// (première liaison par pôle, tri CreatedAt puis Id).
    /// </summary>
    public static async Task EnsureOrgPoleBusinessDepartmentAsync(DirectoryDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE org_poles ADD COLUMN IF NOT EXISTS "BusinessDepartmentId" uuid NULL;
            CREATE INDEX IF NOT EXISTS "IX_org_poles_BusinessDepartmentId" ON org_poles ("BusinessDepartmentId");

            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint WHERE conname = 'FK_org_poles_business_departments_BusinessDepartmentId'
                ) THEN
                    ALTER TABLE org_poles
                        ADD CONSTRAINT "FK_org_poles_business_departments_BusinessDepartmentId"
                        FOREIGN KEY ("BusinessDepartmentId") REFERENCES business_departments("Id") ON DELETE SET NULL;
                END IF;
            END $$;

            UPDATE org_poles p
            SET "BusinessDepartmentId" = sub."BusinessDepartmentId"
            FROM (
                SELECT DISTINCT ON (a."PoleId")
                    a."PoleId",
                    a."BusinessDepartmentId"
                FROM department_pole_assignments a
                ORDER BY a."PoleId", a."CreatedAt" ASC, a."Id" ASC
            ) sub
            WHERE p."Id" = sub."PoleId"
              AND p."BusinessDepartmentId" IS NULL;
            """,
            ct);
    }
}
