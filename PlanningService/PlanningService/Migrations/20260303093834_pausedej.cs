using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanningService.Migrations
{
    /// <inheritdoc />
    public partial class pausedej : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "BreakTime",
                table: "ShiftAssignments",
                type: "time without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BreakTime",
                table: "ShiftAssignments");
        }
    }
}
