using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParrainageBackend.Data.Migrations;

public partial class FixParrainagePortalUserColumnNames : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name = 'parrainage_portal_user'
                      AND column_name = 'id'
                ) THEN
                    ALTER TABLE parrainage_portal_user RENAME COLUMN id TO "Id";
                    ALTER TABLE parrainage_portal_user RENAME COLUMN email TO "Email";
                    ALTER TABLE parrainage_portal_user RENAME COLUMN name TO "Name";
                    ALTER TABLE parrainage_portal_user RENAME COLUMN role TO "Role";
                    ALTER TABLE parrainage_portal_user RENAME COLUMN project_id TO "ProjectId";
                    ALTER TABLE parrainage_portal_user RENAME COLUMN parent_id TO "ParentId";
                    ALTER INDEX IF EXISTS pk_parrainage_portal_user RENAME TO "PK_parrainage_portal_user";
                    ALTER INDEX IF EXISTS ix_parrainage_portal_user_email RENAME TO "IX_parrainage_portal_user_Email";
                END IF;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name = 'parrainage_portal_user'
                      AND column_name = 'Id'
                ) THEN
                    ALTER TABLE parrainage_portal_user RENAME COLUMN "Id" TO id;
                    ALTER TABLE parrainage_portal_user RENAME COLUMN "Email" TO email;
                    ALTER TABLE parrainage_portal_user RENAME COLUMN "Name" TO name;
                    ALTER TABLE parrainage_portal_user RENAME COLUMN "Role" TO role;
                    ALTER TABLE parrainage_portal_user RENAME COLUMN "ProjectId" TO project_id;
                    ALTER TABLE parrainage_portal_user RENAME COLUMN "ParentId" TO parent_id;
                    ALTER INDEX IF EXISTS "PK_parrainage_portal_user" RENAME TO pk_parrainage_portal_user;
                    ALTER INDEX IF EXISTS "IX_parrainage_portal_user_Email" RENAME TO ix_parrainage_portal_user_email;
                END IF;
            END $$;
            """);
    }
}
