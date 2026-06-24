using Microsoft.EntityFrameworkCore;

namespace Prime.Infrastructure.Persistence;

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

        await EnsureValidationQueueRepairAsync(db, ct);
        await EnsureFicheValidationHistoryTableAsync(db, ct);
        await EnsureGlobalPoolScopeSynthesisTablesAsync(db, ct);
    }

    /// <summary>Ajoute les colonnes paiement à la table ligne de synthèse (bases déjà créées).</summary>
    public static async Task EnsureSynthesisLinePaymentColumnsAsync(PrimeDbContext db, CancellationToken ct = default)
    {
        if (!await TableExistsAsync(db, "prime_global_pool_synthesis_line", ct))
            return;

        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE prime_global_pool_synthesis_line
              ADD COLUMN IF NOT EXISTS "PaymentStatus" character varying(32) NOT NULL DEFAULT 'Unpaid';
            ALTER TABLE prime_global_pool_synthesis_line
              ADD COLUMN IF NOT EXISTS "PaidAt" timestamp with time zone;
            ALTER TABLE prime_global_pool_synthesis_line
              ADD COLUMN IF NOT EXISTS "PaidByUserId" character varying(128);
            ALTER TABLE prime_global_pool_synthesis_line
              ADD COLUMN IF NOT EXISTS "PaymentReference" character varying(256);
            """,
            ct);
    }

    /// <summary>Ajoute les colonnes double validation RH/Manager sur les lignes de synthèse (bases déjà créées).</summary>
    public static async Task EnsureSynthesisLineDualDecisionColumnsAsync(PrimeDbContext db, CancellationToken ct = default)
    {
        if (!await TableExistsAsync(db, "prime_global_pool_synthesis_line", ct))
            return;

        // Colonne par colonne (IF NOT EXISTS) : évite les états partiels si RhDecision existe sans ManagerDecidedAt.
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE prime_global_pool_synthesis_line
              ADD COLUMN IF NOT EXISTS "RhDecision" character varying(32) NOT NULL DEFAULT 'Pending';
            ALTER TABLE prime_global_pool_synthesis_line
              ADD COLUMN IF NOT EXISTS "RhDecidedByUserId" character varying(128);
            ALTER TABLE prime_global_pool_synthesis_line
              ADD COLUMN IF NOT EXISTS "RhDecidedAt" timestamp with time zone;
            ALTER TABLE prime_global_pool_synthesis_line
              ADD COLUMN IF NOT EXISTS "RhRejectionReason" character varying(2048);
            ALTER TABLE prime_global_pool_synthesis_line
              ADD COLUMN IF NOT EXISTS "ManagerDecision" character varying(32) NOT NULL DEFAULT 'Pending';
            ALTER TABLE prime_global_pool_synthesis_line
              ADD COLUMN IF NOT EXISTS "ManagerDecidedByUserId" character varying(128);
            ALTER TABLE prime_global_pool_synthesis_line
              ADD COLUMN IF NOT EXISTS "ManagerDecidedAt" timestamp with time zone;
            ALTER TABLE prime_global_pool_synthesis_line
              ADD COLUMN IF NOT EXISTS "ManagerRejectionReason" character varying(2048);
            """,
            ct);
    }

    /// <summary>Correctifs colonnes ligne synthèse (paiement + double validation) — idempotent au démarrage.</summary>
    public static async Task EnsureGlobalPoolSynthesisLineSchemaAsync(PrimeDbContext db, CancellationToken ct = default)
    {
        await EnsureSynthesisLinePaymentColumnsAsync(db, ct);
        await EnsureSynthesisLineDualDecisionColumnsAsync(db, ct);
    }

    /// <summary>
    /// Colonnes snapshot détail fiche employé (migration 20260608140000 non appliquée sur certaines bases).
    /// </summary>
    public static async Task EnsureEmployeeFicheDetailSnapshotColumnsAsync(PrimeDbContext db, CancellationToken ct = default)
    {
        if (!await TableExistsAsync(db, "prime_employee_prime_service_fiche", ct))
            return;

        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE prime_employee_prime_service_fiche
              ADD COLUMN IF NOT EXISTS "DetailGridJson" text;
            ALTER TABLE prime_employee_prime_service_fiche
              ADD COLUMN IF NOT EXISTS "DetailGridPreviewSheetName" character varying(256);
            ALTER TABLE prime_employee_prime_service_fiche
              ADD COLUMN IF NOT EXISTS "TemplateVersionRef" character varying(256);
            ALTER TABLE prime_employee_prime_service_fiche
              ADD COLUMN IF NOT EXISTS "DetailGridFrozenAt" timestamp with time zone;
            """,
            ct);
    }

    public static async Task EnsureGlobalPoolScopeSynthesisTablesAsync(PrimeDbContext db, CancellationToken ct = default)
    {
        if (!await TableExistsAsync(db, "prime_employee_prime_service_fiche", ct))
            return;
        if (await TableExistsAsync(db, "prime_global_pool_scope_synthesis", ct))
            return;

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE prime_global_pool_scope_synthesis (
                "Id" uuid NOT NULL,
                "Period" character varying(16) NOT NULL,
                "ScopeType" character varying(16) NOT NULL,
                "ScopeId" character varying(128) NOT NULL,
                "ScopeDisplayName" character varying(512) NOT NULL DEFAULT '',
                "ExcelContent" bytea,
                "FileName" character varying(512),
                "GeneratedAt" timestamp with time zone,
                "GeneratedByUserId" character varying(128),
                "ManagerApprovedAt" timestamp with time zone,
                "ManagerApprovedByUserId" character varying(128),
                "RhApprovedAt" timestamp with time zone,
                "RhApprovedByUserId" character varying(128),
                "ComptaAckAt" timestamp with time zone,
                "ComptaAckByUserId" character varying(128),
                "UpdatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_prime_global_pool_scope_synthesis" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX "IX_prime_global_pool_scope_synthesis_Period_ScopeType_ScopeId"
                ON prime_global_pool_scope_synthesis ("Period", "ScopeType", "ScopeId");
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE prime_global_pool_synthesis_line (
                "Id" uuid NOT NULL,
                "ScopeSynthesisId" uuid NOT NULL,
                "FicheId" uuid NOT NULL,
                "EmployeeId" character varying(128) NOT NULL,
                "ServiceId" character varying(128) NOT NULL,
                "PrimeAmount" numeric(12,2),
                "ChallengeAmount" numeric(12,2),
                "TotalAmount" numeric(12,2),
                "LineStatus" character varying(32) NOT NULL DEFAULT 'PendingReview',
                "RhDecision" character varying(32) NOT NULL DEFAULT 'Pending',
                "RhDecidedByUserId" character varying(128),
                "RhDecidedAt" timestamp with time zone,
                "RhRejectionReason" character varying(2048),
                "ManagerDecision" character varying(32) NOT NULL DEFAULT 'Pending',
                "ManagerDecidedByUserId" character varying(128),
                "ManagerDecidedAt" timestamp with time zone,
                "ManagerRejectionReason" character varying(2048),
                "RejectedByUserId" character varying(128),
                "RejectedByRole" character varying(64),
                "RejectedAt" timestamp with time zone,
                "RejectionReason" character varying(2048),
                "PaymentStatus" character varying(32) NOT NULL DEFAULT 'Unpaid',
                "PaidAt" timestamp with time zone,
                "PaidByUserId" character varying(128),
                "PaymentReference" character varying(256),
                CONSTRAINT "PK_prime_global_pool_synthesis_line" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_prime_global_pool_synthesis_line_scope"
                    FOREIGN KEY ("ScopeSynthesisId") REFERENCES prime_global_pool_scope_synthesis ("Id") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX "IX_prime_global_pool_synthesis_line_ScopeSynthesisId_FicheId"
                ON prime_global_pool_synthesis_line ("ScopeSynthesisId", "FicheId");
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE prime_global_pool_synthesis_line_history (
                "Id" uuid NOT NULL,
                "LineId" uuid NOT NULL,
                "At" timestamp with time zone NOT NULL,
                "Action" character varying(32) NOT NULL,
                "ActorUserId" character varying(128) NOT NULL,
                "ActorRole" character varying(64) NOT NULL,
                "Comment" character varying(2048),
                CONSTRAINT "PK_prime_global_pool_synthesis_line_history" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_prime_global_pool_synthesis_line_history_line"
                    FOREIGN KEY ("LineId") REFERENCES prime_global_pool_synthesis_line ("Id") ON DELETE CASCADE
            );
            CREATE INDEX "IX_prime_global_pool_synthesis_line_history_LineId_At"
                ON prime_global_pool_synthesis_line_history ("LineId", "At");
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            DO $$
            BEGIN
              IF NOT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'prime_global_pool_approval'
                  AND column_name = 'ScopeSynthesisId'
              ) THEN
                ALTER TABLE prime_global_pool_approval ADD COLUMN "ScopeSynthesisId" uuid;
                ALTER TABLE prime_global_pool_approval ALTER COLUMN "DraftId" DROP NOT NULL;
              END IF;
            END $$;
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            DO $$
            BEGIN
              IF NOT EXISTS (
                SELECT 1 FROM pg_constraint WHERE conname = 'FK_prime_global_pool_approval_scope_synthesis'
              ) THEN
                ALTER TABLE prime_global_pool_approval
                  ADD CONSTRAINT "FK_prime_global_pool_approval_scope_synthesis"
                  FOREIGN KEY ("ScopeSynthesisId")
                  REFERENCES prime_global_pool_scope_synthesis ("Id") ON DELETE CASCADE;
              END IF;
            END $$;
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_prime_global_pool_approval_ScopeSynthesisId_StepId"
                ON prime_global_pool_approval ("ScopeSynthesisId", "StepId")
                WHERE "ScopeSynthesisId" IS NOT NULL;
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20260524140000_GlobalPoolScopeSynthesis', '8.0.11'
            WHERE NOT EXISTS (
                SELECT 1 FROM "__EFMigrationsHistory"
                WHERE "MigrationId" = '20260524140000_GlobalPoolScopeSynthesis');
            """,
            ct);
    }

    /// <summary>
    /// Table historique validation fiche (migration <c>20260524120000</c> sans Designer / base déjà migrée).
    /// </summary>
    public static async Task EnsureFicheValidationHistoryTableAsync(PrimeDbContext db, CancellationToken ct = default)
    {
        if (!await TableExistsAsync(db, "prime_employee_prime_service_fiche", ct))
            return;
        if (await TableExistsAsync(db, "prime_employee_fiche_validation_history", ct))
            return;

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE prime_employee_fiche_validation_history (
                "Id" uuid NOT NULL,
                "FicheId" uuid NOT NULL,
                "At" timestamp with time zone NOT NULL,
                "Action" character varying(32) NOT NULL,
                "FromStatus" character varying(64) NOT NULL,
                "ToStatus" character varying(64) NOT NULL,
                "ActorUserId" character varying(128) NOT NULL,
                "ActorRole" character varying(64) NOT NULL,
                "ActorDisplayName" character varying(256),
                "Comment" character varying(2048),
                "PrimeAmount" numeric(12,2),
                "ChallengeAmount" numeric(12,2),
                "TotalAmount" numeric(12,2),
                CONSTRAINT "PK_prime_employee_fiche_validation_history" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_prime_employee_fiche_validation_history_prime_employee_prime_~"
                    FOREIGN KEY ("FicheId")
                    REFERENCES prime_employee_prime_service_fiche ("Id")
                    ON DELETE CASCADE
            );
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX "IX_prime_employee_fiche_validation_history_FicheId_At"
                ON prime_employee_fiche_validation_history ("FicheId", "At");
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20260524120000_EmployeeFicheValidationHistory', '8.0.11'
            WHERE NOT EXISTS (
                SELECT 1 FROM "__EFMigrationsHistory"
                WHERE "MigrationId" = '20260524120000_EmployeeFicheValidationHistory');
            """,
            ct);
    }

    /// <summary>Rattrapage SQL : fiches complètes + brouillon Validated (même superviseur / cellule / période) → Pending.</summary>
    public static async Task EnsureValidationQueueRepairAsync(PrimeDbContext db, CancellationToken ct = default)
    {
        if (!await TableExistsAsync(db, "prime_employee_prime_service_fiche", ct) ||
            !await TableExistsAsync(db, "prime_supervisor_cellule_prime_draft", ct))
            return;

        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE prime_employee_prime_service_fiche f
            SET "ValidationStatus" = 'Pending', "UpdatedAt" = NOW()
            FROM prime_supervisor_cellule_prime_draft d
            WHERE f."CellulePrimeDraftId" = d."Id"
              AND f."ValidationStatus" IN ('AwaitingData', 'NotStarted')
              AND UPPER(TRIM(f."FillingStatus")) = 'COMPLETE'
              AND UPPER(TRIM(d."Status")) = 'VALIDATED';
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE prime_employee_prime_service_fiche f
            SET "ValidationStatus" = 'Pending',
                "CellulePrimeDraftId" = d."Id",
                "UpdatedAt" = NOW()
            FROM prime_supervisor_cellule_prime_draft d
            WHERE f."SupervisorUserId" = d."SupervisorUserId"
              AND f."CelluleId" = d."CelluleId"
              AND f."Period" = d."Period"
              AND f."ValidationStatus" IN ('AwaitingData', 'NotStarted')
              AND UPPER(TRIM(f."FillingStatus")) = 'COMPLETE'
              AND UPPER(TRIM(d."Status")) = 'VALIDATED'
              AND NOT EXISTS (
                SELECT 1 FROM prime_supervisor_cellule_prime_draft d2
                WHERE d2."Id" = f."CellulePrimeDraftId"
                  AND UPPER(TRIM(d2."Status")) = 'VALIDATED'
              );
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
            "prime_employee_prime_service_fiche" => ScalarBoolAsync(
                db,
                """
                SELECT EXISTS (
                  SELECT 1 FROM information_schema.tables
                  WHERE table_schema = 'public' AND table_name = 'prime_employee_prime_service_fiche');
                """,
                ct),
            "prime_employee_fiche_validation_history" => ScalarBoolAsync(
                db,
                """
                SELECT EXISTS (
                  SELECT 1 FROM information_schema.tables
                  WHERE table_schema = 'public' AND table_name = 'prime_employee_fiche_validation_history');
                """,
                ct),
            "prime_global_pool_scope_synthesis" => ScalarBoolAsync(
                db,
                """
                SELECT EXISTS (
                  SELECT 1 FROM information_schema.tables
                  WHERE table_schema = 'public' AND table_name = 'prime_global_pool_scope_synthesis');
                """,
                ct),
            "prime_global_pool_synthesis_line" => ScalarBoolAsync(
                db,
                """
                SELECT EXISTS (
                  SELECT 1 FROM information_schema.tables
                  WHERE table_schema = 'public' AND table_name = 'prime_global_pool_synthesis_line');
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

    public static async Task EnsureOutboxTableAsync(PrimeDbContext db, CancellationToken ct = default)
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

    public static async Task EnsureAllowanceTrackSchemaAsync(PrimeDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE prime_employee ADD COLUMN IF NOT EXISTS "BusinessDepartmentId" character varying(64) NULL;
            ALTER TABLE prime_employee ADD COLUMN IF NOT EXISTS "BusinessDepartmentKind" character varying(32) NULL;

            CREATE TABLE IF NOT EXISTS prime_business_department (
                "Id" character varying(64) NOT NULL PRIMARY KEY,
                "Code" character varying(64) NOT NULL,
                "Name" character varying(256) NOT NULL,
                "Kind" character varying(32) NOT NULL DEFAULT 'Operational',
                "ManagerEmployeeId" character varying(128) NULL,
                "IsActive" boolean NOT NULL DEFAULT TRUE
            );

            CREATE TABLE IF NOT EXISTS prime_business_department_pole (
                "Id" uuid NOT NULL PRIMARY KEY,
                "BusinessDepartmentId" character varying(64) NOT NULL REFERENCES prime_business_department("Id") ON DELETE CASCADE,
                "PoleId" character varying(64) NOT NULL
            );

            CREATE TABLE IF NOT EXISTS prime_allowance_type (
                "Id" uuid NOT NULL PRIMARY KEY,
                "Code" character varying(64) NOT NULL,
                "Label" character varying(256) NOT NULL,
                "Category" character varying(64) NOT NULL,
                "CalculationMode" character varying(32) NOT NULL DEFAULT 'Manual',
                "DefaultAmount" numeric(18,2) NULL,
                "MinAmount" numeric(18,2) NULL,
                "MaxAmount" numeric(18,2) NULL,
                "RequiresJustification" boolean NOT NULL DEFAULT FALSE,
                "ApplicableDepartmentKinds" character varying(64) NOT NULL DEFAULT 'Support',
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_prime_allowance_type_Code" ON prime_allowance_type ("Code");

            CREATE TABLE IF NOT EXISTS prime_allowance_type_department (
                "Id" uuid NOT NULL PRIMARY KEY,
                "AllowanceTypeId" uuid NOT NULL REFERENCES prime_allowance_type("Id") ON DELETE CASCADE,
                "BusinessDepartmentId" character varying(64) NOT NULL
            );

            CREATE TABLE IF NOT EXISTS prime_allowance_request (
                "Id" uuid NOT NULL PRIMARY KEY,
                "EmployeeId" character varying(128) NOT NULL,
                "BusinessDepartmentId" character varying(64) NOT NULL,
                "AllowanceTypeId" uuid NOT NULL REFERENCES prime_allowance_type("Id"),
                "Period" character varying(16) NOT NULL,
                "Amount" numeric(18,2) NOT NULL,
                "Currency" character varying(8) NOT NULL DEFAULT 'MAD',
                "Reason" character varying(2048) NOT NULL DEFAULT '',
                "Source" character varying(32) NOT NULL DEFAULT 'Manual',
                "Status" character varying(32) NOT NULL DEFAULT 'Draft',
                "CreatedByUserId" character varying(128) NOT NULL,
                "RejectionReason" text NULL,
                "ManagerApprovedByUserId" character varying(128) NULL,
                "ManagerApprovedAt" timestamp with time zone NULL,
                "RhApprovedByUserId" character varying(128) NULL,
                "RhApprovedAt" timestamp with time zone NULL,
                "ComptaApprovedByUserId" character varying(128) NULL,
                "ComptaApprovedAt" timestamp with time zone NULL,
                "PaidAt" timestamp with time zone NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL
            );

            CREATE TABLE IF NOT EXISTS prime_allowance_request_history (
                "Id" uuid NOT NULL PRIMARY KEY,
                "AllowanceRequestId" uuid NOT NULL REFERENCES prime_allowance_request("Id") ON DELETE CASCADE,
                "Action" character varying(32) NOT NULL,
                "FromStatus" character varying(32) NOT NULL,
                "ToStatus" character varying(32) NOT NULL,
                "ActorUserId" character varying(128) NOT NULL,
                "ActorRole" character varying(64) NOT NULL,
                "Comment" text NULL,
                "At" timestamp with time zone NOT NULL
            );

            CREATE TABLE IF NOT EXISTS prime_allowance_workflow_step (
                "Id" uuid NOT NULL PRIMARY KEY,
                "SortOrder" integer NOT NULL,
                "ApproverRole" character varying(64) NOT NULL,
                "IsRequired" boolean NOT NULL DEFAULT TRUE,
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL
            );

            CREATE TABLE IF NOT EXISTS prime_allowance_rule (
                "Id" uuid NOT NULL PRIMARY KEY,
                "AllowanceTypeId" uuid NOT NULL REFERENCES prime_allowance_type("Id"),
                "BusinessDepartmentId" character varying(64) NOT NULL,
                "ConditionJson" text NOT NULL DEFAULT '{{}}',
                "FormulaJson" text NOT NULL DEFAULT '{{}}',
                "DataSource" character varying(64) NOT NULL DEFAULT 'Manual',
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "CreatedAt" timestamp with time zone NOT NULL
            );

            CREATE TABLE IF NOT EXISTS prime_allowance_no_bonus_marker (
                "Id" uuid NOT NULL PRIMARY KEY,
                "EmployeeId" character varying(128) NOT NULL,
                "BusinessDepartmentId" character varying(64) NOT NULL,
                "Period" character varying(16) NOT NULL,
                "MarkedByUserId" character varying(128) NOT NULL,
                "Comment" character varying(512) NULL,
                "CreatedAt" timestamp with time zone NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_prime_allowance_no_bonus_marker_EmployeeId_Period"
                ON prime_allowance_no_bonus_marker ("EmployeeId", "Period");
            """,
            ct);
    }
}
