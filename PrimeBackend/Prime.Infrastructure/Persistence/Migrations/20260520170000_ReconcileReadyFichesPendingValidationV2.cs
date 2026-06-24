using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations;

/// <summary>Rattrapage : fiches complètes + brouillon validé (superviseur/période) → Pending.</summary>
public partial class ReconcileReadyFichesPendingValidationV2 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "prime_employee_prime_service_fiche" f
            SET "ValidationStatus" = 'Pending',
                "CellulePrimeDraftId" = d."Id",
                "UpdatedAt" = NOW()
            FROM "prime_supervisor_cellule_prime_draft" d
            WHERE f."SupervisorUserId" = d."SupervisorUserId"
              AND f."Period" = d."Period"
              AND f."ValidationStatus" IN ('AwaitingData', 'NotStarted')
              AND UPPER(TRIM(f."FillingStatus")) = 'COMPLETE'
              AND UPPER(TRIM(d."Status")) = 'VALIDATED'
              AND (
                f."CellulePrimeDraftId" = d."Id"
                OR f."CelluleId" = d."CelluleId"
                OR d."RootPoleId" = f."CelluleId"
                OR EXISTS (
                  SELECT 1 FROM "prime_cellule" c
                  WHERE c."Id" = f."CelluleId"
                    AND (c."PoleId" = d."RootPoleId" OR c."Id" = d."CelluleId")
                )
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
