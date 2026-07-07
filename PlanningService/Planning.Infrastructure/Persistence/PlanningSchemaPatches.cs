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
                "WeekCode" character varying(32) NOT NULL DEFAULT '',
                "SubServiceName" character varying(200) NOT NULL DEFAULT '',
                "Message" text NOT NULL,
                "IsRead" boolean NOT NULL DEFAULT false,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                "ReadAt" timestamp with time zone NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_PlanningNotifications_AuthUserId" ON "PlanningNotifications" ("AuthUserId");
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
}
