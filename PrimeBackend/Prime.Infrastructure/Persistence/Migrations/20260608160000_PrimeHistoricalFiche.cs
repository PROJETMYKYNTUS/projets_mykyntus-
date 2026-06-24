using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations;

[Migration("20260608160000_PrimeHistoricalFiche")]
public partial class PrimeHistoricalFiche : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "prime_historical_fiche",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Period = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                CelluleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                ServiceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                RootPoleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                SupervisorUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                EmployeeExternalName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                EmployeeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                DetailGridJson = table.Column<string>(type: "text", nullable: true),
                DetailGridPreviewSheetName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                PrimeAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                ChallengeAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                TotalAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                ServiceSaisieJson = table.Column<string>(type: "text", nullable: true),
                OriginFileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                ImportedByUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_prime_historical_fiche", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_prime_historical_fiche_SupervisorUserId_Period",
            table: "prime_historical_fiche",
            columns: new[] { "SupervisorUserId", "Period" });

        migrationBuilder.CreateIndex(
            name: "IX_prime_historical_fiche_CelluleId_Period",
            table: "prime_historical_fiche",
            columns: new[] { "CelluleId", "Period" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "prime_historical_fiche");
    }
}
