using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PlanningService.Migrations
{
    /// <inheritdoc />
    public partial class planning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Declarations_Users_ResolverId",
                table: "Declarations");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftAssignments_Users_UserId",
                table: "ShiftAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_WeeklyPlannings_Users_ValidatorId",
                table: "WeeklyPlannings");

            migrationBuilder.DropIndex(
                name: "IX_WeeklyPlannings_ValidatorId",
                table: "WeeklyPlannings");

            migrationBuilder.DropColumn(
                name: "ValidatorId",
                table: "WeeklyPlannings");

            migrationBuilder.AddColumn<int>(
                name: "RequiredCount",
                table: "WeeklyShiftConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SaturdayGroupId",
                table: "WeeklyPlannings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalEffectif",
                table: "WeeklyPlannings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsManagerOverride",
                table: "ShiftAssignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsNewEmployee",
                table: "ShiftAssignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSaturday",
                table: "ShiftAssignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SaturdayGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    GroupNumber = table.Column<int>(type: "integer", nullable: false),
                    IsNewEmployee = table.Column<bool>(type: "boolean", nullable: false),
                    ManagerOverride = table.Column<bool>(type: "boolean", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AssignedBy = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaturdayGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaturdayGroups_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyPlannings_ValidatedBy",
                table: "WeeklyPlannings",
                column: "ValidatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyPlannings_WeekCode_SubServiceId",
                table: "WeeklyPlannings",
                columns: new[] { "WeekCode", "SubServiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaturdayGroups_UserId",
                table: "SaturdayGroups",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Declarations_Users_ResolverId",
                table: "Declarations",
                column: "ResolverId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftAssignments_Users_UserId",
                table: "ShiftAssignments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WeeklyPlannings_Users_ValidatedBy",
                table: "WeeklyPlannings",
                column: "ValidatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Declarations_Users_ResolverId",
                table: "Declarations");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftAssignments_Users_UserId",
                table: "ShiftAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_WeeklyPlannings_Users_ValidatedBy",
                table: "WeeklyPlannings");

            migrationBuilder.DropTable(
                name: "SaturdayGroups");

            migrationBuilder.DropIndex(
                name: "IX_WeeklyPlannings_ValidatedBy",
                table: "WeeklyPlannings");

            migrationBuilder.DropIndex(
                name: "IX_WeeklyPlannings_WeekCode_SubServiceId",
                table: "WeeklyPlannings");

            migrationBuilder.DropColumn(
                name: "RequiredCount",
                table: "WeeklyShiftConfigs");

            migrationBuilder.DropColumn(
                name: "SaturdayGroupId",
                table: "WeeklyPlannings");

            migrationBuilder.DropColumn(
                name: "TotalEffectif",
                table: "WeeklyPlannings");

            migrationBuilder.DropColumn(
                name: "IsManagerOverride",
                table: "ShiftAssignments");

            migrationBuilder.DropColumn(
                name: "IsNewEmployee",
                table: "ShiftAssignments");

            migrationBuilder.DropColumn(
                name: "IsSaturday",
                table: "ShiftAssignments");

            migrationBuilder.AddColumn<int>(
                name: "ValidatorId",
                table: "WeeklyPlannings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyPlannings_ValidatorId",
                table: "WeeklyPlannings",
                column: "ValidatorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Declarations_Users_ResolverId",
                table: "Declarations",
                column: "ResolverId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftAssignments_Users_UserId",
                table: "ShiftAssignments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WeeklyPlannings_Users_ValidatorId",
                table: "WeeklyPlannings",
                column: "ValidatorId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
