using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations;

[Migration("20260608140000_EmployeeFicheDetailSnapshot")]
public partial class EmployeeFicheDetailSnapshot : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DetailGridJson",
            table: "prime_employee_prime_service_fiche",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DetailGridPreviewSheetName",
            table: "prime_employee_prime_service_fiche",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TemplateVersionRef",
            table: "prime_employee_prime_service_fiche",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DetailGridFrozenAt",
            table: "prime_employee_prime_service_fiche",
            type: "timestamp with time zone",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DetailGridJson", table: "prime_employee_prime_service_fiche");
        migrationBuilder.DropColumn(name: "DetailGridPreviewSheetName", table: "prime_employee_prime_service_fiche");
        migrationBuilder.DropColumn(name: "TemplateVersionRef", table: "prime_employee_prime_service_fiche");
        migrationBuilder.DropColumn(name: "DetailGridFrozenAt", table: "prime_employee_prime_service_fiche");
    }
}
