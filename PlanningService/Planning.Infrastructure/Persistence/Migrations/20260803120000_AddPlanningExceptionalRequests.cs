using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Planning.Infrastructure.Persistence;

#nullable disable

namespace Planning.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260803120000_AddPlanningExceptionalRequests")]
public partial class AddPlanningExceptionalRequests : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PlanningExceptionalRequests",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy",
                        Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                WeekCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                RequestedDate = table.Column<DateOnly>(type: "date", nullable: false),
                RequesterUserId = table.Column<int>(type: "integer", nullable: false),
                SubServiceId = table.Column<int>(type: "integer", nullable: false),
                RequestedShiftTemplateId = table.Column<int>(type: "integer", nullable: false),
                Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                JustificationRequired = table.Column<bool>(type: "boolean", nullable: false),
                JustificationFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                JustificationContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                JustificationContent = table.Column<byte[]>(type: "bytea", nullable: true),
                SupervisorProcessedByUserId = table.Column<int>(type: "integer", nullable: true),
                SupervisorProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                RhProcessedByUserId = table.Column<int>(type: "integer", nullable: true),
                RhProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ProcessedByUserId = table.Column<int>(type: "integer", nullable: true),
                ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlanningExceptionalRequests", x => x.Id);
                table.ForeignKey(
                    name: "FK_PlanningExceptionalRequests_SubServiceShiftConfigs_RequestedShiftTemplateId",
                    column: x => x.RequestedShiftTemplateId,
                    principalTable: "SubServiceShiftConfigs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PlanningExceptionalRequests_SubServices_SubServiceId",
                    column: x => x.SubServiceId,
                    principalTable: "SubServices",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PlanningExceptionalRequests_Users_ProcessedByUserId",
                    column: x => x.ProcessedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_PlanningExceptionalRequests_Users_RequesterUserId",
                    column: x => x.RequesterUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PlanningExceptionalRequests_Users_RhProcessedByUserId",
                    column: x => x.RhProcessedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_PlanningExceptionalRequests_Users_SupervisorProcessedByUserId",
                    column: x => x.SupervisorProcessedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PlanningExceptionalRequests_ProcessedByUserId",
            table: "PlanningExceptionalRequests",
            column: "ProcessedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_PlanningExceptionalRequests_RequestedShiftTemplateId",
            table: "PlanningExceptionalRequests",
            column: "RequestedShiftTemplateId");

        migrationBuilder.CreateIndex(
            name: "IX_PlanningExceptionalRequests_RequesterUserId_CreatedAt",
            table: "PlanningExceptionalRequests",
            columns: new[] { "RequesterUserId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_PlanningExceptionalRequests_RequesterUserId_RequestedDate",
            table: "PlanningExceptionalRequests",
            columns: new[] { "RequesterUserId", "RequestedDate" },
            unique: true,
            filter: "\"Status\" IN (0, 1, 2)");

        migrationBuilder.CreateIndex(
            name: "IX_PlanningExceptionalRequests_RhProcessedByUserId",
            table: "PlanningExceptionalRequests",
            column: "RhProcessedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_PlanningExceptionalRequests_SubServiceId",
            table: "PlanningExceptionalRequests",
            column: "SubServiceId");

        migrationBuilder.CreateIndex(
            name: "IX_PlanningExceptionalRequests_SupervisorProcessedByUserId",
            table: "PlanningExceptionalRequests",
            column: "SupervisorProcessedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_PlanningExceptionalRequests_WeekCode_SubServiceId_Status",
            table: "PlanningExceptionalRequests",
            columns: new[] { "WeekCode", "SubServiceId", "Status" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PlanningExceptionalRequests");
    }
}
