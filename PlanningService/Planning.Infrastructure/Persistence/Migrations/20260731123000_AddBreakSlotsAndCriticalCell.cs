using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planning.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddBreakSlotsAndCriticalCell : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BreakSlotsJson",
            table: "SubServiceShiftConfigs",
            type: "character varying(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsCriticalCell",
            table: "SubServiceShiftConfigs",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "BreakSlotsJson",
            table: "SubServiceShiftConfigs");

        migrationBuilder.DropColumn(
            name: "IsCriticalCell",
            table: "SubServiceShiftConfigs");
    }
}
