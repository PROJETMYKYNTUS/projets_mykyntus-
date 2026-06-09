using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrimeBackend.Data.Migrations;

[Migration("20260609120000_PrimeRejectionReprocessing")]
public partial class PrimeRejectionReprocessing : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "RejectionIsFinal",
            table: "prime_employee_prime_service_fiche",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "RejectedFromStatus",
            table: "prime_employee_prime_service_fiche",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RejectedByRole",
            table: "prime_employee_prime_service_fiche",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "RejectionCount",
            table: "prime_employee_prime_service_fiche",
            type: "integer",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "RejectionIsFinal", table: "prime_employee_prime_service_fiche");
        migrationBuilder.DropColumn(name: "RejectedFromStatus", table: "prime_employee_prime_service_fiche");
        migrationBuilder.DropColumn(name: "RejectedByRole", table: "prime_employee_prime_service_fiche");
        migrationBuilder.DropColumn(name: "RejectionCount", table: "prime_employee_prime_service_fiche");
    }
}
