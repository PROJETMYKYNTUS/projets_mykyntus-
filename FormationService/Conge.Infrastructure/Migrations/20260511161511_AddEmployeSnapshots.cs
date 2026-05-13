using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Conge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "demandes_conge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManagerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeConge = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TypeExceptionnel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DateDebut = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateFin = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NombreJours = table.Column<double>(type: "double precision", nullable: false),
                    Statut = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Motif = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CommentaireManager = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DateDemande = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateDecision = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_demandes_conge", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "employe_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Prenom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ManagerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceNom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DateEmbauche = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EstMineur = table.Column<bool>(type: "boolean", nullable: false),
                    DerniereModification = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employe_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "soldes_conge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Annee = table.Column<int>(type: "integer", nullable: false),
                    SoldeInitial = table.Column<double>(type: "double precision", nullable: false),
                    SoldeUtilise = table.Column<double>(type: "double precision", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DerniereModification = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_soldes_conge", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_demandes_conge_EmployeId",
                table: "demandes_conge",
                column: "EmployeId");

            migrationBuilder.CreateIndex(
                name: "IX_demandes_conge_EmployeId_Statut",
                table: "demandes_conge",
                columns: new[] { "EmployeId", "Statut" });

            migrationBuilder.CreateIndex(
                name: "IX_demandes_conge_ManagerId",
                table: "demandes_conge",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_employe_snapshots_EmployeId",
                table: "employe_snapshots",
                column: "EmployeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employe_snapshots_ManagerId",
                table: "employe_snapshots",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_soldes_conge_EmployeId_Annee",
                table: "soldes_conge",
                columns: new[] { "EmployeId", "Annee" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "demandes_conge");

            migrationBuilder.DropTable(
                name: "employe_snapshots");

            migrationBuilder.DropTable(
                name: "soldes_conge");
        }
    }
}
