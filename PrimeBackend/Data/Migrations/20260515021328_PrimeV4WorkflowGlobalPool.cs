using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrimeBackend.Data.Migrations
{
    /// <inheritdoc />
    public partial class PrimeV4WorkflowGlobalPool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_prime_workflow_step_FromStatus_ToStatus",
                table: "prime_workflow_step");

            migrationBuilder.AddColumn<bool>(
                name: "CapturesAmountsOnApproval",
                table: "prime_workflow_step",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TerminalApproved",
                table: "prime_workflow_step",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "GlobalPoolUploadedByUserId",
                table: "prime_supervisor_cellule_prime_draft",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GlobalPoolRhApprovedByUserId",
                table: "prime_supervisor_cellule_prime_draft",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GlobalPoolManagerApprovedByUserId",
                table: "prime_supervisor_cellule_prime_draft",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GlobalPoolFileName",
                table: "prime_supervisor_cellule_prime_draft",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GlobalPoolComptaAckByUserId",
                table: "prime_supervisor_cellule_prime_draft",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "prime_global_pool_workflow_step",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ApproverRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_global_pool_workflow_step", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prime_global_pool_approval",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_global_pool_approval", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prime_global_pool_approval_prime_global_pool_workflow_step_~",
                        column: x => x.StepId,
                        principalTable: "prime_global_pool_workflow_step",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prime_global_pool_approval_prime_supervisor_cellule_prime_d~",
                        column: x => x.DraftId,
                        principalTable: "prime_supervisor_cellule_prime_draft",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prime_workflow_step_FromStatus_ApproverRole_ToStatus",
                table: "prime_workflow_step",
                columns: new[] { "FromStatus", "ApproverRole", "ToStatus" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prime_global_pool_approval_DraftId_StepId",
                table: "prime_global_pool_approval",
                columns: new[] { "DraftId", "StepId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prime_global_pool_approval_StepId",
                table: "prime_global_pool_approval",
                column: "StepId");

            migrationBuilder.CreateIndex(
                name: "IX_prime_global_pool_workflow_step_SortOrder",
                table: "prime_global_pool_workflow_step",
                column: "SortOrder");

            migrationBuilder.Sql("""
                UPDATE "prime_employee" SET "Role" = 'Comptabilité' WHERE "Role" = 'Comptable';
                UPDATE "prime_rbac_permission" SET "Role" = 'Comptabilité' WHERE "Role" = 'Comptable';
                UPDATE "prime_workflow_step" SET "CapturesAmountsOnApproval" = true
                  WHERE "ApproverRole" = 'Superviseur' AND "FromStatus" = 'Pending';
                UPDATE "prime_workflow_step" SET "TerminalApproved" = true
                  WHERE "ToStatus" = 'RH Approved';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prime_global_pool_approval");

            migrationBuilder.DropTable(
                name: "prime_global_pool_workflow_step");

            migrationBuilder.DropIndex(
                name: "IX_prime_workflow_step_FromStatus_ApproverRole_ToStatus",
                table: "prime_workflow_step");

            migrationBuilder.DropColumn(
                name: "CapturesAmountsOnApproval",
                table: "prime_workflow_step");

            migrationBuilder.DropColumn(
                name: "TerminalApproved",
                table: "prime_workflow_step");

            migrationBuilder.AlterColumn<string>(
                name: "GlobalPoolUploadedByUserId",
                table: "prime_supervisor_cellule_prime_draft",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GlobalPoolRhApprovedByUserId",
                table: "prime_supervisor_cellule_prime_draft",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GlobalPoolManagerApprovedByUserId",
                table: "prime_supervisor_cellule_prime_draft",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GlobalPoolFileName",
                table: "prime_supervisor_cellule_prime_draft",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GlobalPoolComptaAckByUserId",
                table: "prime_supervisor_cellule_prime_draft",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_prime_workflow_step_FromStatus_ToStatus",
                table: "prime_workflow_step",
                columns: new[] { "FromStatus", "ToStatus" },
                unique: true);
        }
    }
}
