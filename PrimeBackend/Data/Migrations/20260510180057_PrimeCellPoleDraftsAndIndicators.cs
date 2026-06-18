using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrimeBackend.Data.Migrations
{
    /// <inheritdoc />
    public partial class PrimeCellPoleDraftsAndIndicators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "prime_cellule_prime_indicator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CelluleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    PonderationPct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TemplateStableId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_cellule_prime_indicator", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prime_cellule_prime_indicator_prime_cellule_CelluleId",
                        column: x => x.CelluleId,
                        principalTable: "prime_cellule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prime_supervisor_pole_prime_draft",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupervisorUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PoleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Period = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TemplateId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TemplateDisplayName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    TemplateFormatVersion = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SchemaJson = table.Column<string>(type: "text", nullable: false),
                    PoleSaisieJson = table.Column<string>(type: "text", nullable: false),
                    ComputedJson = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_supervisor_pole_prime_draft", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prime_employee_prime_cell_fiche",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PolePrimeDraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupervisorUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EmployeeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CelluleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PoleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Period = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CellSaisieJson = table.Column<string>(type: "text", nullable: false),
                    FillingStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_employee_prime_cell_fiche", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prime_employee_prime_cell_fiche_prime_supervisor_pole_prime~",
                        column: x => x.PolePrimeDraftId,
                        principalTable: "prime_supervisor_pole_prime_draft",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prime_employee_CelluleId",
                table: "prime_employee",
                column: "CelluleId");

            migrationBuilder.CreateIndex(
                name: "IX_prime_cellule_prime_indicator_CelluleId",
                table: "prime_cellule_prime_indicator",
                column: "CelluleId");

            migrationBuilder.CreateIndex(
                name: "IX_prime_cellule_prime_indicator_CelluleId_SortOrder",
                table: "prime_cellule_prime_indicator",
                columns: new[] { "CelluleId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_prime_employee_prime_cell_fiche_CelluleId_Period",
                table: "prime_employee_prime_cell_fiche",
                columns: new[] { "CelluleId", "Period" });

            migrationBuilder.CreateIndex(
                name: "IX_prime_employee_prime_cell_fiche_EmployeeId_Period",
                table: "prime_employee_prime_cell_fiche",
                columns: new[] { "EmployeeId", "Period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prime_employee_prime_cell_fiche_PolePrimeDraftId",
                table: "prime_employee_prime_cell_fiche",
                column: "PolePrimeDraftId");

            migrationBuilder.CreateIndex(
                name: "IX_prime_employee_prime_cell_fiche_SupervisorUserId_Period",
                table: "prime_employee_prime_cell_fiche",
                columns: new[] { "SupervisorUserId", "Period" });

            migrationBuilder.CreateIndex(
                name: "IX_prime_supervisor_pole_prime_draft_SupervisorUserId_Period",
                table: "prime_supervisor_pole_prime_draft",
                columns: new[] { "SupervisorUserId", "Period" });

            migrationBuilder.CreateIndex(
                name: "IX_prime_supervisor_pole_prime_draft_SupervisorUserId_PoleId_P~",
                table: "prime_supervisor_pole_prime_draft",
                columns: new[] { "SupervisorUserId", "PoleId", "Period", "TemplateId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prime_cellule_prime_indicator");

            migrationBuilder.DropTable(
                name: "prime_employee_prime_cell_fiche");

            migrationBuilder.DropTable(
                name: "prime_supervisor_pole_prime_draft");

            migrationBuilder.DropIndex(
                name: "IX_prime_employee_CelluleId",
                table: "prime_employee");
        }
    }
}
