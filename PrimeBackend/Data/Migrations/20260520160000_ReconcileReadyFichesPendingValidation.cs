using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrimeBackend.Data.Migrations;

/// <summary>
/// Passe en Pending les fiches déjà complètes (partie cellule) dont le brouillon pôle est Validated.
/// </summary>
public partial class ReconcileReadyFichesPendingValidation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "prime_employee_prime_service_fiche" f
            SET "ValidationStatus" = 'Pending', "UpdatedAt" = NOW()
            FROM "prime_supervisor_cellule_prime_draft" d
            WHERE f."CellulePrimeDraftId" = d."Id"
              AND f."ValidationStatus" IN ('AwaitingData', 'NotStarted')
              AND UPPER(TRIM(f."FillingStatus")) = 'COMPLETE'
              AND UPPER(TRIM(d."Status")) = 'VALIDATED';
            """);

        migrationBuilder.Sql(
            """
            UPDATE "prime_employee_prime_service_fiche" f
            SET "ValidationStatus" = 'Pending',
                "CellulePrimeDraftId" = d."Id",
                "UpdatedAt" = NOW()
            FROM "prime_supervisor_cellule_prime_draft" d
            WHERE f."SupervisorUserId" = d."SupervisorUserId"
              AND f."CelluleId" = d."CelluleId"
              AND f."Period" = d."Period"
              AND f."ValidationStatus" IN ('AwaitingData', 'NotStarted')
              AND UPPER(TRIM(f."FillingStatus")) = 'COMPLETE'
              AND UPPER(TRIM(d."Status")) = 'VALIDATED'
              AND NOT EXISTS (
                SELECT 1 FROM "prime_supervisor_cellule_prime_draft" d2
                WHERE d2."Id" = f."CellulePrimeDraftId"
                  AND UPPER(TRIM(d2."Status")) = 'VALIDATED'
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Pas de rollback automatique — statuts gérés par la logique métier.
    }
}
