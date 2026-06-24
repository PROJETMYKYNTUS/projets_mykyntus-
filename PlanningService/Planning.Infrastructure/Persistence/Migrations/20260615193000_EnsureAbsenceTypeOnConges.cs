using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Planning.Infrastructure.Persistence;

#nullable disable

namespace Planning.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260615193000_EnsureAbsenceTypeOnConges")]
public partial class EnsureAbsenceTypeOnConges : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AbsenceType",
            table: "Conges",
            type: "integer",
            nullable: false,
            defaultValue: 5);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AbsenceType",
            table: "Conges");
    }
}
