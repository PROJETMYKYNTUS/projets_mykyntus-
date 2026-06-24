using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAllowanceTrack : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BusinessDepartmentId",
            table: "prime_employee",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "BusinessDepartmentKind",
            table: "prime_employee",
            type: "character varying(32)",
            maxLength: 32,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "BusinessDepartmentId", table: "prime_employee");
        migrationBuilder.DropColumn(name: "BusinessDepartmentKind", table: "prime_employee");
    }
}
