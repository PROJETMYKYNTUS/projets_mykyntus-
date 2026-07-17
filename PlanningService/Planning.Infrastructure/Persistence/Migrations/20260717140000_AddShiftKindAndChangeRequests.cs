using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Planning.Infrastructure.Persistence;

#nullable disable

namespace Planning.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260717140000_AddShiftKindAndChangeRequests")]
public partial class AddShiftKindAndChangeRequests : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ShiftKind",
            table: "SubServiceShiftConfigs",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "PlanningChangeRequests",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                WeekCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                RequesterUserId = table.Column<int>(type: "integer", nullable: false),
                CurrentAssignmentId = table.Column<int>(type: "integer", nullable: false),
                Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                ProposedSwapUserId = table.Column<int>(type: "integer", nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ProcessedByUserId = table.Column<int>(type: "integer", nullable: true),
                ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlanningChangeRequests", x => x.Id);
                table.ForeignKey(
                    name: "FK_PlanningChangeRequests_ShiftAssignments_CurrentAssignmentId",
                    column: x => x.CurrentAssignmentId,
                    principalTable: "ShiftAssignments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PlanningChangeRequests_Users_ProcessedByUserId",
                    column: x => x.ProcessedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_PlanningChangeRequests_Users_ProposedSwapUserId",
                    column: x => x.ProposedSwapUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_PlanningChangeRequests_Users_RequesterUserId",
                    column: x => x.RequesterUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PlanningChangeRequests_CurrentAssignmentId",
            table: "PlanningChangeRequests",
            column: "CurrentAssignmentId");

        migrationBuilder.CreateIndex(
            name: "IX_PlanningChangeRequests_ProcessedByUserId",
            table: "PlanningChangeRequests",
            column: "ProcessedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_PlanningChangeRequests_ProposedSwapUserId",
            table: "PlanningChangeRequests",
            column: "ProposedSwapUserId");

        migrationBuilder.CreateIndex(
            name: "IX_PlanningChangeRequests_RequesterUserId_WeekCode",
            table: "PlanningChangeRequests",
            columns: new[] { "RequesterUserId", "WeekCode" });

        migrationBuilder.CreateIndex(
            name: "IX_PlanningChangeRequests_Status_WeekCode",
            table: "PlanningChangeRequests",
            columns: new[] { "Status", "WeekCode" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PlanningChangeRequests");
        migrationBuilder.DropColumn(name: "ShiftKind", table: "SubServiceShiftConfigs");
    }
}
