using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Formation.Infrastructure.Persistence;

/// <summary>
/// Patches SQL idempotents quand une migration EF n'a pas été appliquée
/// (ex. migration sans attribut [Migration] / snapshot incomplet).
/// </summary>
public static class FormationSchemaPatches
{
    public static async Task EnsureTrainingWorkflowTablesAsync(
        FormationDbContext db,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS training_sessions (
                    "Id" uuid NOT NULL,
                    "Title" character varying(300) NOT NULL,
                    "Description" text NOT NULL,
                    "Type" integer NOT NULL,
                    "AnimatorKind" integer NOT NULL,
                    "AnimatorUserId" uuid NULL,
                    "ExternalAnimatorName" text NULL,
                    "ExternalAnimatorOrganization" text NULL,
                    "ExternalAnimatorEmail" text NULL,
                    "ExternalAnimatorPhone" text NULL,
                    "PlannedStart" timestamp with time zone NOT NULL,
                    "PlannedEnd" timestamp with time zone NOT NULL,
                    "Capacity" integer NOT NULL,
                    "Status" integer NOT NULL,
                    "CreatedByUserId" text NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_training_sessions" PRIMARY KEY ("Id")
                );
                """,
                ct);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS training_assignments (
                    "Id" uuid NOT NULL,
                    "SessionId" uuid NOT NULL,
                    "EmployeeId" uuid NOT NULL,
                    "EmployeeName" text NOT NULL,
                    "Status" integer NOT NULL,
                    "AssignedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_training_assignments" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_training_assignments_training_sessions_SessionId"
                        FOREIGN KEY ("SessionId") REFERENCES training_sessions ("Id") ON DELETE CASCADE
                );
                """,
                ct);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_training_assignments_SessionId_EmployeeId"
                    ON training_assignments ("SessionId", "EmployeeId");
                """,
                ct);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS initial_training_paths (
                    "Id" uuid NOT NULL,
                    "EmployeeId" uuid NOT NULL,
                    "EmployeeName" text NOT NULL,
                    "DateDebut" timestamp with time zone NOT NULL,
                    "DateFinPrevue" timestamp with time zone NOT NULL,
                    "Status" integer NOT NULL,
                    "QuizScore" numeric NULL,
                    "QuizPassed" boolean NULL,
                    "QuizRecordedBy" text NULL,
                    "FormateurComment" text NULL,
                    "FormateurValidatedAt" timestamp with time zone NULL,
                    "RhValidatedAt" timestamp with time zone NULL,
                    "RejectedBy" text NULL,
                    "RejectReason" text NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_initial_training_paths" PRIMARY KEY ("Id")
                );
                """,
                ct);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_initial_training_paths_EmployeeId"
                    ON initial_training_paths ("EmployeeId");
                """,
                ct);
        }
        catch (Exception ex)
        {
            // Tables déjà présentes / droits partiels : on vérifie l'essentiel.
            var exists = await TableExistsAsync(db, "training_sessions", ct);
            if (exists)
            {
                logger?.LogWarning(ex, "Patch training partiel ignoré — training_sessions existe déjà.");
                return;
            }

            throw;
        }
    }

    private static async Task<bool> TableExistsAsync(
        FormationDbContext db,
        string tableName,
        CancellationToken ct)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        if (cmd.Connection!.State != System.Data.ConnectionState.Open)
            await cmd.Connection.OpenAsync(ct);
        cmd.CommandText = """
            SELECT EXISTS (
              SELECT 1 FROM information_schema.tables
              WHERE table_schema = 'public' AND table_name = @name
            );
            """;
        var p = cmd.CreateParameter();
        p.ParameterName = "name";
        p.Value = tableName;
        cmd.Parameters.Add(p);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is true || result is bool b && b;
    }
}
