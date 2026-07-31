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
                CREATE TABLE IF NOT EXISTS training_programs (
                    "Id" uuid NOT NULL,
                    "Title" character varying(300) NOT NULL,
                    "Description" text NOT NULL,
                    "Mode" integer NOT NULL,
                    "SessionCount" integer NOT NULL,
                    "AnimatorKind" integer NOT NULL,
                    "AnimatorUserId" uuid NULL,
                    "ExternalAnimatorName" text NULL,
                    "ExternalAnimatorOrganization" text NULL,
                    "ExternalAnimatorEmail" text NULL,
                    "ExternalAnimatorPhone" text NULL,
                    "Capacity" integer NOT NULL,
                    "CreatedByUserId" text NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_training_programs" PRIMARY KEY ("Id")
                );
                """,
                ct);

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

            await EnsureColumnAsync(db, "training_sessions", "ProgramId", "uuid NULL", ct);
            await EnsureColumnAsync(db, "training_sessions", "SequenceNumber", "integer NOT NULL DEFAULT 1", ct);

            await db.Database.ExecuteSqlRawAsync(
                """
                DO $$ BEGIN
                  IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint WHERE conname = 'FK_training_sessions_training_programs_ProgramId'
                  ) THEN
                    ALTER TABLE training_sessions
                      ADD CONSTRAINT "FK_training_sessions_training_programs_ProgramId"
                      FOREIGN KEY ("ProgramId") REFERENCES training_programs ("Id") ON DELETE SET NULL;
                  END IF;
                END $$;
                """,
                ct);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_training_sessions_ProgramId"
                    ON training_sessions ("ProgramId");
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

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS training_session_reports (
                    "Id" uuid NOT NULL,
                    "SessionId" uuid NOT NULL,
                    "UploadedByUserId" uuid NOT NULL,
                    "FileName" character varying(500) NOT NULL,
                    "ContentType" character varying(200) NOT NULL,
                    "StoragePath" character varying(1000) NOT NULL,
                    "UploadedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_training_session_reports" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_training_session_reports_training_sessions_SessionId"
                        FOREIGN KEY ("SessionId") REFERENCES training_sessions ("Id") ON DELETE CASCADE
                );
                """,
                ct);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_training_session_reports_SessionId"
                    ON training_session_reports ("SessionId");
                """,
                ct);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS training_quizzes (
                    "Id" uuid NOT NULL,
                    "SessionId" uuid NOT NULL,
                    "Title" character varying(300) NOT NULL,
                    "Status" integer NOT NULL,
                    "CreatedByUserId" uuid NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "ValidatedByUserId" uuid NULL,
                    "ValidatedAt" timestamp with time zone NULL,
                    "RejectedByUserId" uuid NULL,
                    "RejectedAt" timestamp with time zone NULL,
                    "RejectedReason" text NULL,
                    CONSTRAINT "PK_training_quizzes" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_training_quizzes_training_sessions_SessionId"
                        FOREIGN KEY ("SessionId") REFERENCES training_sessions ("Id") ON DELETE CASCADE
                );
                """,
                ct);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_training_quizzes_SessionId"
                    ON training_quizzes ("SessionId");
                """,
                ct);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS training_quiz_questions (
                    "Id" uuid NOT NULL,
                    "QuizId" uuid NOT NULL,
                    "SortOrder" integer NOT NULL,
                    "Type" integer NOT NULL,
                    "Prompt" text NOT NULL,
                    "OptionsJson" text NULL,
                    "CorrectOptionIndex" integer NULL,
                    "Points" numeric(18,2) NOT NULL,
                    CONSTRAINT "PK_training_quiz_questions" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_training_quiz_questions_training_quizzes_QuizId"
                        FOREIGN KEY ("QuizId") REFERENCES training_quizzes ("Id") ON DELETE CASCADE
                );
                """,
                ct);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS training_quiz_attempts (
                    "Id" uuid NOT NULL,
                    "QuizId" uuid NOT NULL,
                    "AssignmentId" uuid NOT NULL,
                    "EmployeeId" uuid NOT NULL,
                    "AnswersJson" text NOT NULL,
                    "AutoScore" numeric(18,2) NULL,
                    "ManualScore" numeric(18,2) NULL,
                    "FinalScore" numeric(18,2) NULL,
                    "Passed" boolean NULL,
                    "IsGraded" boolean NOT NULL DEFAULT FALSE,
                    "GradedByUserId" uuid NULL,
                    "GradedAt" timestamp with time zone NULL,
                    "AnimatorComment" text NULL,
                    "SubmittedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_training_quiz_attempts" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_training_quiz_attempts_training_quizzes_QuizId"
                        FOREIGN KEY ("QuizId") REFERENCES training_quizzes ("Id") ON DELETE CASCADE
                );
                """,
                ct);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_training_quiz_attempts_QuizId_AssignmentId"
                    ON training_quiz_attempts ("QuizId", "AssignmentId");
                """,
                ct);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS initial_training_quiz_results (
                    "Id" uuid NOT NULL,
                    "InitialTrainingPathId" uuid NOT NULL,
                    "Title" character varying(300) NOT NULL,
                    "Score" numeric(18,2) NOT NULL,
                    "Passed" boolean NOT NULL,
                    "RecordedBy" character varying(200) NULL,
                    "RecordedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_initial_training_quiz_results" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_initial_training_quiz_results_initial_training_paths_InitialTrainingPathId"
                        FOREIGN KEY ("InitialTrainingPathId") REFERENCES initial_training_paths ("Id") ON DELETE CASCADE
                );
                """,
                ct);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_initial_training_quiz_results_InitialTrainingPathId"
                    ON initial_training_quiz_results ("InitialTrainingPathId");
                """,
                ct);

            // Migration douce : une ligne « Quiz » depuis QuizScore legacy s'il n'existe encore aucun résultat.
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO initial_training_quiz_results
                    ("Id", "InitialTrainingPathId", "Title", "Score", "Passed", "RecordedBy", "RecordedAt")
                SELECT
                    gen_random_uuid(),
                    p."Id",
                    'Quiz',
                    p."QuizScore",
                    COALESCE(p."QuizPassed", FALSE),
                    p."QuizRecordedBy",
                    COALESCE(p."UpdatedAt", NOW())
                FROM initial_training_paths p
                WHERE p."QuizScore" IS NOT NULL
                  AND NOT EXISTS (
                    SELECT 1 FROM initial_training_quiz_results r
                    WHERE r."InitialTrainingPathId" = p."Id"
                  );
                """,
                ct);
        }
        catch (Exception ex)
        {
            var exists = await TableExistsAsync(db, "training_sessions", ct);
            if (exists)
            {
                logger?.LogWarning(ex, "Patch training partiel ignoré — training_sessions existe déjà.");
                return;
            }

            throw;
        }
    }

    public static async Task EnsureDocumentChecklistTablesAsync(
        FormationDbContext db,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS formation_document_definitions (
                "Id" uuid NOT NULL,
                "Title" character varying(300) NOT NULL,
                "SortOrder" integer NOT NULL,
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "CreatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_formation_document_definitions" PRIMARY KEY ("Id")
            );
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS formation_document_checklist_items (
                "Id" uuid NOT NULL,
                "EmployeeId" uuid NOT NULL,
                "InitialTrainingPathId" uuid NULL,
                "DefinitionId" uuid NOT NULL,
                "IsReceived" boolean NOT NULL DEFAULT FALSE,
                "ReceivedAt" timestamp with time zone NULL,
                "ReceivedBy" character varying(200) NULL,
                "Note" text NULL,
                CONSTRAINT "PK_formation_document_checklist_items" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_formation_document_checklist_items_definitions"
                    FOREIGN KEY ("DefinitionId") REFERENCES formation_document_definitions ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_formation_document_checklist_items_paths"
                    FOREIGN KEY ("InitialTrainingPathId") REFERENCES initial_training_paths ("Id") ON DELETE CASCADE
            );
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_formation_document_checklist_items_EmployeeId"
                ON formation_document_checklist_items ("EmployeeId");
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_formation_document_checklist_items_InitialTrainingPathId"
                ON formation_document_checklist_items ("InitialTrainingPathId");
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_formation_document_checklist_items_Path_Definition"
                ON formation_document_checklist_items ("InitialTrainingPathId", "DefinitionId");
            """,
            ct);

        logger?.LogInformation("Tables checklist documents formation vérifiées.");
    }

    public static async Task EnsureQuizMultiChoiceColumnsAsync(
        FormationDbContext db,
        CancellationToken ct = default)
    {
        await EnsureColumnAsync(db, "training_quiz_questions", "AllowMultiple", "boolean NOT NULL DEFAULT FALSE", ct);
        await EnsureColumnAsync(db, "training_quiz_questions", "CorrectOptionIndexesJson", "text NULL", ct);
        await EnsureColumnAsync(db, "training_quizzes", "PassThreshold", "numeric(18,2) NOT NULL DEFAULT 70", ct);
        await EnsureColumnAsync(db, "training_quiz_attempts", "FreeTextGradesJson", "text NULL", ct);
        await EnsureColumnAsync(db, "training_quiz_questions", "ImageUrl", "character varying(2000) NULL", ct);
        await EnsureColumnAsync(db, "training_quiz_questions", "ImageStoragePath", "character varying(1000) NULL", ct);
        await EnsureColumnAsync(db, "training_quiz_questions", "Explanation", "text NULL", ct);
        await EnsureColumnAsync(db, "training_quizzes", "AllowMultipleAttempts", "boolean NOT NULL DEFAULT FALSE", ct);
        await EnsureColumnAsync(db, "training_quiz_attempts", "AttemptNumber", "integer NOT NULL DEFAULT 1", ct);
        await EnsureColumnAsync(db, "training_sessions", "CatalogItemId", "uuid NULL", ct);
        await EnsureColumnAsync(db, "training_sessions", "LearningGateMode", "integer NULL", ct);
        await EnsureColumnAsync(db, "employe_annuaires", "StructureKey", "character varying(200) NULL", ct);

        // Replace legacy unique (QuizId, AssignmentId) with (QuizId, AssignmentId, AttemptNumber).
        await db.Database.ExecuteSqlRawAsync(
            """
            DO $$ BEGIN
              IF EXISTS (
                SELECT 1 FROM pg_indexes
                WHERE schemaname = 'public'
                  AND indexname = 'IX_training_quiz_attempts_QuizId_AssignmentId'
              ) THEN
                DROP INDEX IF EXISTS "IX_training_quiz_attempts_QuizId_AssignmentId";
              END IF;
              IF NOT EXISTS (
                SELECT 1 FROM pg_indexes
                WHERE schemaname = 'public'
                  AND indexname = 'IX_training_quiz_attempts_QuizId_AssignmentId_AttemptNumber'
              ) THEN
                CREATE UNIQUE INDEX "IX_training_quiz_attempts_QuizId_AssignmentId_AttemptNumber"
                  ON training_quiz_attempts ("QuizId", "AssignmentId", "AttemptNumber");
              END IF;
            END $$;
            """,
            ct);
    }

    public static async Task EnsureLearningCatalogTablesAsync(
        FormationDbContext db,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS training_catalog_items (
                "Id" uuid NOT NULL,
                "Title" character varying(300) NOT NULL,
                "Description" text NOT NULL DEFAULT '',
                "Category" character varying(200) NOT NULL DEFAULT '',
                "Status" integer NOT NULL DEFAULT 0,
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "DefaultGateMode" integer NOT NULL DEFAULT 1,
                "AudienceMatchMode" integer NOT NULL DEFAULT 0,
                "CreatedByUserId" character varying(100) NOT NULL DEFAULT '',
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "PublishedAt" timestamp with time zone NULL,
                "ArchivedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_training_catalog_items" PRIMARY KEY ("Id")
            );
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS training_modules (
                "Id" uuid NOT NULL,
                "CatalogItemId" uuid NOT NULL,
                "Title" character varying(300) NOT NULL,
                "Description" text NOT NULL DEFAULT '',
                "SortOrder" integer NOT NULL DEFAULT 0,
                CONSTRAINT "PK_training_modules" PRIMARY KEY ("Id")
            );
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS training_lessons (
                "Id" uuid NOT NULL,
                "ModuleId" uuid NOT NULL,
                "Title" character varying(300) NOT NULL,
                "Description" text NOT NULL DEFAULT '',
                "SortOrder" integer NOT NULL DEFAULT 0,
                "IsRequired" boolean NOT NULL DEFAULT TRUE,
                CONSTRAINT "PK_training_lessons" PRIMARY KEY ("Id")
            );
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS training_resources (
                "Id" uuid NOT NULL,
                "LessonId" uuid NOT NULL,
                "Type" integer NOT NULL DEFAULT 0,
                "Title" character varying(300) NOT NULL,
                "Url" character varying(2000) NULL,
                "StoragePath" character varying(1000) NULL,
                "ContentType" character varying(200) NULL,
                "FileName" character varying(500) NULL,
                "TextContent" text NULL,
                "SortOrder" integer NOT NULL DEFAULT 0,
                "DurationMinutes" integer NULL,
                CONSTRAINT "PK_training_resources" PRIMARY KEY ("Id")
            );
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS training_catalog_audience_rules (
                "Id" uuid NOT NULL,
                "CatalogItemId" uuid NOT NULL,
                "RolesJson" text NOT NULL DEFAULT '[]',
                "StructureKeysJson" text NOT NULL DEFAULT '[]',
                "UserIdsJson" text NOT NULL DEFAULT '[]',
                CONSTRAINT "PK_training_catalog_audience_rules" PRIMARY KEY ("Id")
            );
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS training_lesson_progress (
                "Id" uuid NOT NULL,
                "AssignmentId" uuid NOT NULL,
                "LessonId" uuid NOT NULL,
                "LastResourceId" uuid NULL,
                "ProgressPercent" numeric(18,2) NOT NULL DEFAULT 0,
                "StartedAt" timestamp with time zone NOT NULL,
                "CompletedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_training_lesson_progress" PRIMARY KEY ("Id")
            );
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_training_modules_CatalogItemId" ON training_modules ("CatalogItemId");
            CREATE INDEX IF NOT EXISTS "IX_training_lessons_ModuleId" ON training_lessons ("ModuleId");
            CREATE INDEX IF NOT EXISTS "IX_training_resources_LessonId" ON training_resources ("LessonId");
            CREATE INDEX IF NOT EXISTS "IX_training_catalog_audience_rules_CatalogItemId" ON training_catalog_audience_rules ("CatalogItemId");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_training_lesson_progress_AssignmentId_LessonId"
                ON training_lesson_progress ("AssignmentId", "LessonId");
            CREATE INDEX IF NOT EXISTS "IX_training_sessions_CatalogItemId" ON training_sessions ("CatalogItemId");
            CREATE INDEX IF NOT EXISTS "IX_training_catalog_items_Category" ON training_catalog_items ("Category");
            CREATE INDEX IF NOT EXISTS "IX_training_catalog_items_Status" ON training_catalog_items ("Status");
            """,
            ct);

        logger?.LogInformation("Tables catalogue e-learning formation vérifiées.");
    }

    private static async Task EnsureColumnAsync(
        FormationDbContext db,
        string table,
        string column,
        string definition,
        CancellationToken ct)
    {
        // Identifiants figés par les patches (pas d'entrée utilisateur).
        var sql =
            "DO $$ BEGIN " +
            "IF NOT EXISTS ( " +
            "SELECT 1 FROM information_schema.columns " +
            "WHERE table_schema = 'public' AND table_name = '" + table + "' AND column_name = '" + column + "' " +
            ") THEN " +
            "ALTER TABLE " + table + " ADD COLUMN \"" + column + "\" " + definition + "; " +
            "END IF; END $$;";
        await db.Database.ExecuteSqlRawAsync(sql, ct);
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
