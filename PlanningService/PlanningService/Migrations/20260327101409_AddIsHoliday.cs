using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PlanningService.Migrations
{
    /// <inheritdoc />
    public partial class AddIsHoliday : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHoliday",
                table: "ShiftAssignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SaturdayHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    SubServiceId = table.Column<int>(type: "integer", nullable: false),
                    WeekCode = table.Column<string>(type: "text", nullable: false),
                    WorkedSaturday = table.Column<bool>(type: "boolean", nullable: false),
                    IsManualEntry = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaturdayHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaturdayHistories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaturdayHistories_UserId",
                table: "SaturdayHistories",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SaturdayHistories");

            migrationBuilder.DropColumn(
                name: "IsHoliday",
                table: "ShiftAssignments");
        }
    }
}
