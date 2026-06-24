using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
[Migration("20260524120000_EmployeeFicheValidationHistory")]
public partial class EmployeeFicheValidationHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "prime_employee_fiche_validation_history",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                FicheId = table.Column<Guid>(type: "uuid", nullable: false),
                At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                FromStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ToStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ActorUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                ActorRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ActorDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                Comment = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                PrimeAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                ChallengeAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                TotalAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_prime_employee_fiche_validation_history", x => x.Id);
                table.ForeignKey(
                    name: "FK_prime_employee_fiche_validation_history_prime_employee_prime_~",
                    column: x => x.FicheId,
                    principalTable: "prime_employee_prime_service_fiche",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_prime_employee_fiche_validation_history_FicheId_At",
            table: "prime_employee_fiche_validation_history",
            columns: new[] { "FicheId", "At" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "prime_employee_fiche_validation_history");
    }
}
