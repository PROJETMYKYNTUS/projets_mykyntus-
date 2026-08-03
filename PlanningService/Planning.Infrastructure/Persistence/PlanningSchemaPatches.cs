using Microsoft.EntityFrameworkCore;

namespace Planning.Infrastructure.Persistence;

public static class PlanningSchemaPatches
{
    public static async Task EnsureOutboxTableAsync(AppDbContext db, CancellationToken ct = default)
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

    public static async Task EnsurePlanningNotificationsTableAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "PlanningNotifications" (
                "Id" serial PRIMARY KEY,
                "UserId" integer NOT NULL,
                "AuthUserId" integer NOT NULL,
                "WeeklyPlanningId" integer NULL,
                "WeekCode" character varying(64) NOT NULL DEFAULT '',
                "SubServiceName" character varying(200) NOT NULL DEFAULT '',
                "Message" text NOT NULL,
                "IsRead" boolean NOT NULL DEFAULT false,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                "ReadAt" timestamp with time zone NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_PlanningNotifications_AuthUserId" ON "PlanningNotifications" ("AuthUserId");
            ALTER TABLE "PlanningNotifications" ALTER COLUMN "WeekCode" TYPE character varying(64);
            """,
            ct);
    }

    public static async Task EnsureUserHrProfilesTableAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS user_hr_profiles (
                "UserId" integer NOT NULL PRIMARY KEY,
                "ChefDeProjetId" uuid NULL,
                "SuperviseurId" uuid NULL,
                "ReferentTechniqueId" uuid NULL,
                "DateNaissance" date NULL,
                "VilleNaissance" character varying(200) NULL,
                "Nationalite" character varying(100) NULL,
                "Sexe" character varying(20) NULL,
                "SituationFamiliale" character varying(50) NULL,
                "NombreEnfants" integer NULL,
                "Cin" character varying(50) NULL,
                "Adresse" text NULL,
                "Telephone1" character varying(50) NULL,
                "TelephoneUrgence" character varying(50) NULL,
                "RelationUrgence" character varying(100) NULL,
                "Rib" character varying(34) NULL,
                "ImmatriculationInterne" character varying(50) NULL,
                "ImmatriculationCnss" character varying(50) NULL,
                "DateEntree" date NULL,
                "DateEmbauche" date NULL,
                "DateAnciennete" date NULL,
                "DateSortie" date NULL,
                "DateEvolutionPoste" date NULL,
                "AncienPoste" character varying(200) NULL,
                "AncienService" character varying(200) NULL,
                "NiveauScolaire" character varying(100) NULL,
                "IntitulesEtudes" text NULL,
                "EnFormation" boolean NOT NULL DEFAULT false,
                "DateDebutFormation" date NULL,
                "DateFinFormationPrevue" date NULL,
                "NiveauExpertiseMetier" integer NULL,
                "UpdatedAt" timestamp with time zone NOT NULL DEFAULT now()
            );
            """,
            ct);
    }

    public static async Task EnsureDateDebutFormationColumnAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE user_hr_profiles ADD COLUMN IF NOT EXISTS "DateDebutFormation" date NULL;
            """,
            ct);
    }

    public static async Task EnsureNumeroCarteAutoentrepreneurColumnAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE user_hr_profiles ADD COLUMN IF NOT EXISTS "NumeroCarteAutoentrepreneur" character varying(64) NULL;
            """,
            ct);
    }

    public static async Task EnsureEmailPersonnelColumnAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE user_hr_profiles ADD COLUMN IF NOT EXISTS "EmailPersonnel" character varying(256) NULL;
            """,
            ct);
    }

    public static async Task EnsureCongeSourceDemandeIdColumnAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "Conges" ADD COLUMN IF NOT EXISTS "SourceDemandeId" uuid NULL;
            CREATE INDEX IF NOT EXISTS "IX_Conges_SourceDemandeId" ON "Conges" ("SourceDemandeId");
            """,
            ct);
    }

    /// <summary>
    /// Template shifts + consultations + auto-generate settings (idempotent).
    /// </summary>
    public static async Task EnsureShiftTemplateAndValidationSchemaAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "SubServiceShiftConfigs" ADD COLUMN IF NOT EXISTS "IsTemplate" boolean NOT NULL DEFAULT false;

            ALTER TABLE "SubServiceShiftConfigs" ALTER COLUMN "WeekCode" DROP NOT NULL;
            ALTER TABLE "SubServiceShiftConfigs" ALTER COLUMN "WeekStartDate" DROP NOT NULL;

            -- Clone latest week configs into permanent templates (keep original rows as snapshots)
            INSERT INTO "SubServiceShiftConfigs" (
                "SubServiceId", "WeekCode", "WeekStartDate", "IsTemplate",
                "Label", "StartTime", "WorkHours",
                "BreakRangeStart", "BreakRangeEnd", "BreakDurationMinutes",
                "RequiredCount", "Percentage", "MinPresencePercent", "DisplayOrder", "CreatedAt"
            )
            SELECT
                c."SubServiceId", NULL, NULL, true,
                c."Label", c."StartTime", c."WorkHours",
                c."BreakRangeStart", c."BreakRangeEnd", c."BreakDurationMinutes",
                c."RequiredCount", c."Percentage", c."MinPresencePercent", c."DisplayOrder", now()
            FROM "SubServiceShiftConfigs" c
            INNER JOIN (
                SELECT "SubServiceId", MAX("WeekStartDate") AS max_start
                FROM "SubServiceShiftConfigs"
                WHERE "WeekCode" IS NOT NULL
                GROUP BY "SubServiceId"
            ) latest ON latest."SubServiceId" = c."SubServiceId" AND latest.max_start = c."WeekStartDate"
            WHERE COALESCE(c."IsTemplate", false) = false
              AND NOT EXISTS (
                SELECT 1 FROM "SubServiceShiftConfigs" t
                WHERE t."SubServiceId" = c."SubServiceId" AND t."IsTemplate" = true
              );
            DROP INDEX IF EXISTS "IX_SubServiceShiftConfigs_SubServiceId_WeekCode_Label";

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_SubServiceShiftConfigs_Template_SubServiceId_Label"
              ON "SubServiceShiftConfigs" ("SubServiceId", "Label")
              WHERE "IsTemplate" = TRUE;

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_SubServiceShiftConfigs_Snapshot_SubServiceId_WeekCode_Label"
              ON "SubServiceShiftConfigs" ("SubServiceId", "WeekCode", "Label")
              WHERE "IsTemplate" = FALSE AND "WeekCode" IS NOT NULL;

            CREATE TABLE IF NOT EXISTS "PlanningConsultations" (
                "Id" serial PRIMARY KEY,
                "PlanningId" integer NOT NULL,
                "UserId" integer NOT NULL,
                "ConsultedAt" timestamp with time zone NOT NULL DEFAULT now(),
                CONSTRAINT "FK_PlanningConsultations_WeeklyPlannings_PlanningId"
                    FOREIGN KEY ("PlanningId") REFERENCES "WeeklyPlannings" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_PlanningConsultations_Users_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_PlanningConsultations_PlanningId_UserId"
              ON "PlanningConsultations" ("PlanningId", "UserId");

            CREATE TABLE IF NOT EXISTS "PlanningAutoGenerateSettings" (
                "Id" character varying(32) NOT NULL PRIMARY KEY,
                "Enabled" boolean NOT NULL DEFAULT true,
                "DayOfWeek" integer NOT NULL DEFAULT 4,
                "HourLocal" integer NOT NULL DEFAULT 6,
                "MinuteLocal" integer NOT NULL DEFAULT 0,
                "TimeZone" character varying(64) NOT NULL DEFAULT 'Africa/Casablanca',
                "Target" character varying(32) NOT NULL DEFAULT 'NextWeek',
                "LastRunAt" timestamp with time zone NULL,
                "LastRunWeekCode" character varying(16) NULL,
                "UpdatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                "UpdatedByUserId" integer NULL
            );

            INSERT INTO "PlanningAutoGenerateSettings" ("Id", "Enabled", "DayOfWeek", "HourLocal", "MinuteLocal", "TimeZone", "Target", "UpdatedAt")
            VALUES ('default', true, 4, 6, 0, 'Africa/Casablanca', 'NextWeek', now())
            ON CONFLICT ("Id") DO NOTHING;
            """,
            ct);
    }

    /// <summary>Colonne ShiftKind sur SubServiceShiftConfigs (idempotent).</summary>
    public static async Task EnsureShiftKindColumnAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "SubServiceShiftConfigs" ADD COLUMN IF NOT EXISTS "ShiftKind" integer NOT NULL DEFAULT 0;
            """,
            ct);
    }

    /// <summary>BreakSlotsJson + IsCriticalCell sur SubServiceShiftConfigs (idempotent).</summary>
    public static async Task EnsureBreakSlotsAndCriticalCellAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "SubServiceShiftConfigs" ADD COLUMN IF NOT EXISTS "BreakSlotsJson" character varying(128) NULL;
            ALTER TABLE "SubServiceShiftConfigs" ADD COLUMN IF NOT EXISTS "IsCriticalCell" boolean NOT NULL DEFAULT false;
            """,
            ct);
    }

    /// <summary>Table PlanningChangeRequests (idempotent) + colonnes workflow switch.</summary>
    public static async Task EnsurePlanningChangeRequestsTableAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "PlanningChangeRequests" (
                "Id" serial PRIMARY KEY,
                "WeekCode" character varying(16) NOT NULL,
                "RequesterUserId" integer NOT NULL,
                "CurrentAssignmentId" integer NOT NULL,
                "Reason" character varying(1000) NOT NULL,
                "ProposedSwapUserId" integer NULL,
                "Status" integer NOT NULL DEFAULT 0,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                "PartnerRespondedAt" timestamp with time zone NULL,
                "SupervisorProcessedByUserId" integer NULL,
                "ProcessedByUserId" integer NULL,
                "ProcessedAt" timestamp with time zone NULL,
                "RejectionReason" character varying(1000) NULL,
                CONSTRAINT "FK_PlanningChangeRequests_Users_Requester"
                    FOREIGN KEY ("RequesterUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_PlanningChangeRequests_ShiftAssignments"
                    FOREIGN KEY ("CurrentAssignmentId") REFERENCES "ShiftAssignments" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_PlanningChangeRequests_Users_Proposed"
                    FOREIGN KEY ("ProposedSwapUserId") REFERENCES "Users" ("Id") ON DELETE SET NULL,
                CONSTRAINT "FK_PlanningChangeRequests_Users_Processed"
                    FOREIGN KEY ("ProcessedByUserId") REFERENCES "Users" ("Id") ON DELETE SET NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_PlanningChangeRequests_Requester_WeekCode"
              ON "PlanningChangeRequests" ("RequesterUserId", "WeekCode");
            CREATE INDEX IF NOT EXISTS "IX_PlanningChangeRequests_Status_WeekCode"
              ON "PlanningChangeRequests" ("Status", "WeekCode");

            ALTER TABLE "PlanningChangeRequests"
              ADD COLUMN IF NOT EXISTS "PartnerRespondedAt" timestamp with time zone NULL;
            ALTER TABLE "PlanningChangeRequests"
              ADD COLUMN IF NOT EXISTS "SupervisorProcessedByUserId" integer NULL;
            """,
            ct);
    }

    public static async Task EnsureEmployeeImportSourceFileColumnsAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "EmployeeImportSessions"
              ADD COLUMN IF NOT EXISTS "FileContent" bytea NULL;
            ALTER TABLE "EmployeeImportSessions"
              ADD COLUMN IF NOT EXISTS "ContentType" character varying(128) NULL;
            ALTER TABLE "EmployeeImportJobs"
              ADD COLUMN IF NOT EXISTS "FileContent" bytea NULL;
            ALTER TABLE "EmployeeImportJobs"
              ADD COLUMN IF NOT EXISTS "ContentType" character varying(128) NULL;
            ALTER TABLE "EmployeeImportJobs"
              ADD COLUMN IF NOT EXISTS "ProcessedLignes" integer NOT NULL DEFAULT 0;
            ALTER TABLE "EmployeeImportJobs"
              ADD COLUMN IF NOT EXISTS "Status" character varying(32) NOT NULL DEFAULT 'Completed';
            ALTER TABLE "EmployeeImportJobs"
              ADD COLUMN IF NOT EXISTS "ErrorMessage" text NULL;
            """,
            ct);
    }

    public static async Task EnsureUserHtelColumnsAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "IdTechnicien" integer NULL;
            ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "HtelCode" character varying(128) NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_IdTechnicien"
                ON "Users" ("IdTechnicien")
                WHERE "IdTechnicien" IS NOT NULL;
            """,
            ct);
    }

    /// <summary>Table PlanningExceptionalRequests (idempotent).</summary>
    public static async Task EnsurePlanningExceptionalRequestsTableAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "PlanningExceptionalRequests" (
                "Id" serial PRIMARY KEY,
                "WeekCode" character varying(16) NOT NULL,
                "RequestedDate" date NOT NULL,
                "RequesterUserId" integer NOT NULL,
                "SubServiceId" integer NOT NULL,
                "RequestedShiftTemplateId" integer NOT NULL,
                "Reason" character varying(1000) NOT NULL,
                "Status" integer NOT NULL DEFAULT 0,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                "JustificationRequired" boolean NOT NULL DEFAULT false,
                "JustificationFileName" character varying(260) NULL,
                "JustificationContentType" character varying(128) NULL,
                "JustificationContent" bytea NULL,
                "SupervisorProcessedByUserId" integer NULL,
                "SupervisorProcessedAt" timestamp with time zone NULL,
                "RhProcessedByUserId" integer NULL,
                "RhProcessedAt" timestamp with time zone NULL,
                "ProcessedByUserId" integer NULL,
                "ProcessedAt" timestamp with time zone NULL,
                "RejectionReason" character varying(1000) NULL,
                CONSTRAINT "FK_PlanningExceptionalRequests_Users_Requester"
                    FOREIGN KEY ("RequesterUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_PlanningExceptionalRequests_SubServices"
                    FOREIGN KEY ("SubServiceId") REFERENCES "SubServices" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_PlanningExceptionalRequests_ShiftTemplate"
                    FOREIGN KEY ("RequestedShiftTemplateId") REFERENCES "SubServiceShiftConfigs" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_PlanningExceptionalRequests_Users_Supervisor"
                    FOREIGN KEY ("SupervisorProcessedByUserId") REFERENCES "Users" ("Id") ON DELETE SET NULL,
                CONSTRAINT "FK_PlanningExceptionalRequests_Users_Rh"
                    FOREIGN KEY ("RhProcessedByUserId") REFERENCES "Users" ("Id") ON DELETE SET NULL,
                CONSTRAINT "FK_PlanningExceptionalRequests_Users_Processed"
                    FOREIGN KEY ("ProcessedByUserId") REFERENCES "Users" ("Id") ON DELETE SET NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_PlanningExceptionalRequests_Week_Sub_Status"
              ON "PlanningExceptionalRequests" ("WeekCode", "SubServiceId", "Status");
            CREATE INDEX IF NOT EXISTS "IX_PlanningExceptionalRequests_Requester_Created"
              ON "PlanningExceptionalRequests" ("RequesterUserId", "CreatedAt");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_PlanningExceptionalRequests_Active_User_Date"
              ON "PlanningExceptionalRequests" ("RequesterUserId", "RequestedDate")
              WHERE "Status" IN (0, 1, 2);
            """,
            ct);
    }

    /// <summary>Colonne ShiftAssignments.IsExceptionalRequest (tag DE sur la grille).</summary>
    public static async Task EnsureShiftAssignmentExceptionalFlagAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "ShiftAssignments"
              ADD COLUMN IF NOT EXISTS "IsExceptionalRequest" boolean NOT NULL DEFAULT false;
            """,
            ct);
    }
}
