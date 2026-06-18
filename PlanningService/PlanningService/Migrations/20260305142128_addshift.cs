using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PlanningService.Migrations
{
    /// <inheritdoc />
    public partial class addshift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SubServiceShiftConfigId",
                table: "ShiftAssignments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SubServiceShiftConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubServiceId = table.Column<int>(type: "integer", nullable: false),
                    WeekCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    WeekStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    WorkHours = table.Column<int>(type: "integer", nullable: false),
                    BreakRangeStart = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    BreakRangeEnd = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    BreakDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    RequiredCount = table.Column<int>(type: "integer", nullable: false),
                    Percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    MinPresencePercent = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubServiceShiftConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubServiceShiftConfigs_SubServices_SubServiceId",
                        column: x => x.SubServiceId,
                        principalTable: "SubServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_SubServiceShiftConfigId",
                table: "ShiftAssignments",
                column: "SubServiceShiftConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_SubServiceShiftConfigs_SubServiceId_WeekCode_Label",
                table: "SubServiceShiftConfigs",
                columns: new[] { "SubServiceId", "WeekCode", "Label" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftAssignments_SubServiceShiftConfigs_SubServiceShiftConf~",
                table: "ShiftAssignments",
                column: "SubServiceShiftConfigId",
                principalTable: "SubServiceShiftConfigs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShiftAssignments_SubServiceShiftConfigs_SubServiceShiftConf~",
                table: "ShiftAssignments");

            migrationBuilder.DropTable(
                name: "SubServiceShiftConfigs");

            migrationBuilder.DropIndex(
                name: "IX_ShiftAssignments_SubServiceShiftConfigId",
                table: "ShiftAssignments");

            migrationBuilder.DropColumn(
                name: "SubServiceShiftConfigId",
                table: "ShiftAssignments");
        }
    }
}
