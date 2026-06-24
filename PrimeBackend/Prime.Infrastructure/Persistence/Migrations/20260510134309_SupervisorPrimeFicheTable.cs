using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupervisorPrimeFicheTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "prime_supervisor_fiche",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupervisorUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PoleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Period = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TemplateId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TemplateDisplayName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    TemplateFormatVersion = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SchemaJson = table.Column<string>(type: "text", nullable: false),
                    SaisieJson = table.Column<string>(type: "text", nullable: false),
                    ComputedJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_supervisor_fiche", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prime_supervisor_fiche_SupervisorUserId_Period",
                table: "prime_supervisor_fiche",
                columns: new[] { "SupervisorUserId", "Period" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prime_supervisor_fiche");
        }
    }
}
