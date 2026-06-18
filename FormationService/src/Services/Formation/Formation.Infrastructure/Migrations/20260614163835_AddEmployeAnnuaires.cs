using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeAnnuaires : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "employe_annuaires",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Prenom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ManagerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DerniereModification = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employe_annuaires", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_employe_annuaires_Email",
                table: "employe_annuaires",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_employe_annuaires_EmployeId",
                table: "employe_annuaires",
                column: "EmployeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employe_annuaires");
        }
    }
}
