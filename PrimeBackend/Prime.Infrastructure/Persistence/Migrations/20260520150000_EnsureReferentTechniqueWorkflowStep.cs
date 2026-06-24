using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations;

/// <summary>
/// Rétablit l’étape Référent technique (Pending → Référent technique Approved) si la migration V3
/// n’a laissé que Superviseur en première position.
/// </summary>
public partial class EnsureReferentTechniqueWorkflowStep : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "prime_workflow_step"
            SET "IsActive" = FALSE, "UpdatedAt" = NOW()
            WHERE "ApproverRole" IN ('RH', 'Manager', 'Comptabilité', 'Comptable', 'Admin', 'Audit', 'Pilote');

            INSERT INTO "prime_workflow_step" (
                "Id", "SortOrder", "ApproverRole", "FromStatus", "ToStatus",
                "IsActive", "SlaHours", "CapturesAmountsOnApproval", "TerminalApproved", "CreatedAt", "UpdatedAt")
            SELECT gen_random_uuid(), 1, 'Référent technique', 'Pending', 'Référent technique Approved',
                   TRUE, 48, TRUE, FALSE, NOW(), NULL
            WHERE NOT EXISTS (
                SELECT 1 FROM "prime_workflow_step"
                WHERE "IsActive" = TRUE
                  AND "FromStatus" = 'Pending'
                  AND "ApproverRole" IN ('Référent technique', 'Coach')
            );

            UPDATE "prime_workflow_step"
            SET "SortOrder" = 2, "FromStatus" = 'Référent technique Approved', "ToStatus" = 'Superviseur Approved',
                "IsActive" = TRUE, "UpdatedAt" = NOW()
            WHERE "ApproverRole" = 'Superviseur'
              AND EXISTS (SELECT 1 FROM "prime_workflow_step" WHERE "IsActive" = TRUE AND "FromStatus" = 'Pending' AND "ApproverRole" IN ('Référent technique', 'Coach'));

            UPDATE "prime_workflow_step"
            SET "SortOrder" = 3, "FromStatus" = 'Superviseur Approved', "ToStatus" = 'Chef de projet Approved',
                "TerminalApproved" = TRUE, "IsActive" = TRUE, "UpdatedAt" = NOW()
            WHERE "ApproverRole" IN ('Chef de projet', 'RP');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Pas de rollback automatique — configuration workflow gérée par l’admin / seeder.
    }
}
