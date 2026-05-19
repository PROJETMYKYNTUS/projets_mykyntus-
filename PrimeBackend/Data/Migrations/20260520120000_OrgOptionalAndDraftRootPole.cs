using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrimeBackend.Data.Migrations;

/// <inheritdoc />
public partial class OrgOptionalAndDraftRootPole : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_prime_supervisor_cellule_prime_draft_SupervisorUserId_CelluleId_Period_TemplateId",
            table: "prime_supervisor_cellule_prime_draft");

        migrationBuilder.AlterColumn<string>(
            name: "CelluleId",
            table: "prime_employee",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text");

        migrationBuilder.AlterColumn<string>(
            name: "ServiceId",
            table: "prime_employee",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text");

        migrationBuilder.AddColumn<string>(
            name: "RootPoleId",
            table: "prime_supervisor_cellule_prime_draft",
            type: "character varying(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE prime_supervisor_cellule_prime_draft AS d
            SET "RootPoleId" = c."PoleId"
            FROM prime_cellule AS c
            WHERE d."CelluleId" = c."Id";
            """);

        migrationBuilder.Sql(
            """
            UPDATE prime_supervisor_cellule_prime_draft AS d
            SET "RootPoleId" = d."CelluleId"
            WHERE (d."RootPoleId" IS NULL OR d."RootPoleId" = '')
              AND EXISTS (SELECT 1 FROM prime_pole p WHERE p."Id" = d."CelluleId");
            """);

        migrationBuilder.Sql(
            """
            DELETE FROM prime_supervisor_cellule_prime_draft
            WHERE "RootPoleId" IS NULL OR "RootPoleId" = ''
               OR NOT EXISTS (SELECT 1 FROM prime_pole p WHERE p."Id" = "RootPoleId");
            """);

        migrationBuilder.AlterColumn<string>(
            name: "RootPoleId",
            table: "prime_supervisor_cellule_prime_draft",
            type: "character varying(128)",
            maxLength: 128,
            nullable: false,
            defaultValue: "");

        migrationBuilder.Sql(
            """
            DELETE FROM prime_supervisor_cellule_prime_draft a
            USING prime_supervisor_cellule_prime_draft b
            WHERE a.ctid < b.ctid
              AND a."SupervisorUserId" = b."SupervisorUserId"
              AND a."RootPoleId" = b."RootPoleId"
              AND a."Period" = b."Period";
            """);

        migrationBuilder.CreateIndex(
            name: "IX_prime_supervisor_cellule_prime_draft_SupervisorUserId_RootPoleId_Period",
            table: "prime_supervisor_cellule_prime_draft",
            columns: new[] { "SupervisorUserId", "RootPoleId", "Period" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_prime_supervisor_cellule_prime_draft_SupervisorUserId_CelluleId_Period_TemplateId",
            table: "prime_supervisor_cellule_prime_draft",
            columns: new[] { "SupervisorUserId", "CelluleId", "Period", "TemplateId" });

        migrationBuilder.CreateIndex(
            name: "IX_prime_supervisor_cellule_prime_draft_RootPoleId",
            table: "prime_supervisor_cellule_prime_draft",
            column: "RootPoleId");

        migrationBuilder.AddForeignKey(
            name: "FK_prime_supervisor_cellule_prime_draft_prime_pole_RootPoleId",
            table: "prime_supervisor_cellule_prime_draft",
            column: "RootPoleId",
            principalTable: "prime_pole",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_prime_supervisor_cellule_prime_draft_prime_pole_RootPoleId",
            table: "prime_supervisor_cellule_prime_draft");

        migrationBuilder.DropIndex(
            name: "IX_prime_supervisor_cellule_prime_draft_RootPoleId",
            table: "prime_supervisor_cellule_prime_draft");

        migrationBuilder.DropIndex(
            name: "IX_prime_supervisor_cellule_prime_draft_SupervisorUserId_CelluleId_Period_TemplateId",
            table: "prime_supervisor_cellule_prime_draft");

        migrationBuilder.DropIndex(
            name: "IX_prime_supervisor_cellule_prime_draft_SupervisorUserId_RootPoleId_Period",
            table: "prime_supervisor_cellule_prime_draft");

        migrationBuilder.DropColumn(
            name: "RootPoleId",
            table: "prime_supervisor_cellule_prime_draft");

        migrationBuilder.AlterColumn<string>(
            name: "ServiceId",
            table: "prime_employee",
            type: "text",
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "CelluleId",
            table: "prime_employee",
            type: "text",
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_prime_supervisor_cellule_prime_draft_SupervisorUserId_CelluleId_Period_TemplateId",
            table: "prime_supervisor_cellule_prime_draft",
            columns: new[] { "SupervisorUserId", "CelluleId", "Period", "TemplateId" },
            unique: true);
    }
}
