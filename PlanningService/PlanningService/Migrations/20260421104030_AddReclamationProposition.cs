using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PlanningService.Migrations
{
    /// <inheritdoc />
    public partial class AddReclamationProposition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Propositions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Titre = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    BeneficeAttendu = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Priorite = table.Column<int>(type: "integer", nullable: false),
                    AuteurId = table.Column<string>(type: "text", nullable: false),
                    AuteurNom = table.Column<string>(type: "text", nullable: false),
                    AuteurRole = table.Column<string>(type: "text", nullable: false),
                    AssigneeId = table.Column<string>(type: "text", nullable: true),
                    AssigneeNom = table.Column<string>(type: "text", nullable: true),
                    CommentaireEvaluation = table.Column<string>(type: "text", nullable: true),
                    SatisfactionNote = table.Column<int>(type: "integer", nullable: true),
                    SatisfactionCommentaire = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EvalueeAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ImplementeeAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Propositions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reclamations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Titre = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Priorite = table.Column<int>(type: "integer", nullable: false),
                    AuteurId = table.Column<string>(type: "text", nullable: false),
                    AuteurNom = table.Column<string>(type: "text", nullable: false),
                    AuteurRole = table.Column<string>(type: "text", nullable: false),
                    AssigneeId = table.Column<string>(type: "text", nullable: true),
                    AssigneeNom = table.Column<string>(type: "text", nullable: true),
                    CommentaireTraitement = table.Column<string>(type: "text", nullable: true),
                    SatisfactionNote = table.Column<int>(type: "integer", nullable: true),
                    SatisfactionCommentaire = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TraiteeAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ClotureeAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reclamations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PropositionHistoriques",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PropositionId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Valeur = table.Column<string>(type: "text", nullable: false),
                    EffectueParId = table.Column<string>(type: "text", nullable: false),
                    EffectueParNom = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropositionHistoriques", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropositionHistoriques_Propositions_PropositionId",
                        column: x => x.PropositionId,
                        principalTable: "Propositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReclamationHistoriques",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReclamationId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Valeur = table.Column<string>(type: "text", nullable: false),
                    EffectueParId = table.Column<string>(type: "text", nullable: false),
                    EffectueParNom = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReclamationHistoriques", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReclamationHistoriques_Reclamations_ReclamationId",
                        column: x => x.ReclamationId,
                        principalTable: "Reclamations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropositionHistoriques_PropositionId",
                table: "PropositionHistoriques",
                column: "PropositionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReclamationHistoriques_ReclamationId",
                table: "ReclamationHistoriques",
                column: "ReclamationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PropositionHistoriques");

            migrationBuilder.DropTable(
                name: "ReclamationHistoriques");

            migrationBuilder.DropTable(
                name: "Propositions");

            migrationBuilder.DropTable(
                name: "Reclamations");
        }
    }
}
