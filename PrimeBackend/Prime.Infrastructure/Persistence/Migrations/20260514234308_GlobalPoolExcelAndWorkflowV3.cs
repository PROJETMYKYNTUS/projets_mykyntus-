using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GlobalPoolExcelAndWorkflowV3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GlobalPoolComptaAckAt",
                table: "prime_supervisor_cellule_prime_draft",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GlobalPoolComptaAckByUserId",
                table: "prime_supervisor_cellule_prime_draft",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "GlobalPoolExcelContent",
                table: "prime_supervisor_cellule_prime_draft",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GlobalPoolFileName",
                table: "prime_supervisor_cellule_prime_draft",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GlobalPoolManagerApprovedAt",
                table: "prime_supervisor_cellule_prime_draft",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GlobalPoolManagerApprovedByUserId",
                table: "prime_supervisor_cellule_prime_draft",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GlobalPoolRhApprovedAt",
                table: "prime_supervisor_cellule_prime_draft",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GlobalPoolRhApprovedByUserId",
                table: "prime_supervisor_cellule_prime_draft",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GlobalPoolUploadedAt",
                table: "prime_supervisor_cellule_prime_draft",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GlobalPoolUploadedByUserId",
                table: "prime_supervisor_cellule_prime_draft",
                type: "text",
                nullable: true);

            // Ancien flux : « Référent technique Approved » n’est plus une étape — aligner sur « Superviseur Approved ».
            migrationBuilder.Sql(
                """
                UPDATE "prime_employee_prime_service_fiche"
                SET "ValidationStatus" = 'Superviseur Approved'
                WHERE "ValidationStatus" = 'Référent technique Approved';
                """);

            // Re-seed des étapes workflow (suppression des 4 lignes incluant l’étape RT).
            migrationBuilder.Sql("""DELETE FROM "prime_workflow_step";""");
            migrationBuilder.Sql(
                """
                INSERT INTO "prime_workflow_step" ("Id", "SortOrder", "ApproverRole", "FromStatus", "ToStatus", "IsActive", "SlaHours", "CreatedAt", "UpdatedAt")
                VALUES
                  (gen_random_uuid(), 1, 'Superviseur', 'Pending', 'Superviseur Approved', TRUE, 48, NOW(), NULL),
                  (gen_random_uuid(), 2, 'Chef de projet', 'Superviseur Approved', 'Chef de projet Approved', TRUE, 72, NOW(), NULL),
                  (gen_random_uuid(), 3, 'RH', 'Chef de projet Approved', 'RH Approved', TRUE, 72, NOW(), NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GlobalPoolComptaAckAt",
                table: "prime_supervisor_cellule_prime_draft");

            migrationBuilder.DropColumn(
                name: "GlobalPoolComptaAckByUserId",
                table: "prime_supervisor_cellule_prime_draft");

            migrationBuilder.DropColumn(
                name: "GlobalPoolExcelContent",
                table: "prime_supervisor_cellule_prime_draft");

            migrationBuilder.DropColumn(
                name: "GlobalPoolFileName",
                table: "prime_supervisor_cellule_prime_draft");

            migrationBuilder.DropColumn(
                name: "GlobalPoolManagerApprovedAt",
                table: "prime_supervisor_cellule_prime_draft");

            migrationBuilder.DropColumn(
                name: "GlobalPoolManagerApprovedByUserId",
                table: "prime_supervisor_cellule_prime_draft");

            migrationBuilder.DropColumn(
                name: "GlobalPoolRhApprovedAt",
                table: "prime_supervisor_cellule_prime_draft");

            migrationBuilder.DropColumn(
                name: "GlobalPoolRhApprovedByUserId",
                table: "prime_supervisor_cellule_prime_draft");

            migrationBuilder.DropColumn(
                name: "GlobalPoolUploadedAt",
                table: "prime_supervisor_cellule_prime_draft");

            migrationBuilder.DropColumn(
                name: "GlobalPoolUploadedByUserId",
                table: "prime_supervisor_cellule_prime_draft");
        }
    }
}
