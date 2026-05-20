using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrimeBackend.Data.Migrations;

/// <inheritdoc />
public partial class EmployeeFicheAwaitingDataValidationStatus : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "ValidationStatus",
            table: "prime_employee_prime_service_fiche",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "AwaitingData",
            oldClrType: typeof(string),
            oldType: "character varying(64)",
            oldMaxLength: 64);

        migrationBuilder.Sql(
            """
            UPDATE "prime_employee_prime_service_fiche" f
            SET "ValidationStatus" = 'AwaitingData'
            FROM "prime_supervisor_cellule_prime_draft" d
            WHERE f."CellulePrimeDraftId" = d."Id"
              AND f."ValidationStatus" = 'Pending'
              AND f."LastApprovedAt" IS NULL
              AND (
                UPPER(d."Status") <> 'VALIDATED'
                OR UPPER(f."FillingStatus") <> 'COMPLETE'
              );
            """);

        migrationBuilder.Sql(
            """
            UPDATE "prime_employee_prime_service_fiche" f
            SET "ValidationStatus" = 'Pending'
            FROM "prime_supervisor_cellule_prime_draft" d
            WHERE f."CellulePrimeDraftId" = d."Id"
              AND f."ValidationStatus" IN ('AwaitingData', 'NotStarted')
              AND UPPER(d."Status") = 'VALIDATED'
              AND UPPER(f."FillingStatus") = 'COMPLETE';
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "prime_employee_prime_service_fiche"
            SET "ValidationStatus" = 'Pending'
            WHERE "ValidationStatus" = 'AwaitingData'
              AND "LastApprovedAt" IS NULL;
            """);

        migrationBuilder.AlterColumn<string>(
            name: "ValidationStatus",
            table: "prime_employee_prime_service_fiche",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(64)",
            oldMaxLength: 64,
            oldDefaultValue: "AwaitingData");
    }
}
