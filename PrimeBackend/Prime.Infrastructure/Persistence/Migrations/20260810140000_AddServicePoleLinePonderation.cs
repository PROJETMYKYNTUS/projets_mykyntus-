using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations;

[Migration("20260810140000_AddServicePoleLinePonderation")]
public partial class AddServicePoleLinePonderation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "prime_service_pole_line_ponderation",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ServiceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                TemplateStableId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Label = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                PonderationPrimePct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                PonderationChallengePct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_prime_service_pole_line_ponderation", x => x.Id);
                table.ForeignKey(
                    name: "FK_prime_service_pole_line_ponderation_prime_service_ServiceId",
                    column: x => x.ServiceId,
                    principalTable: "prime_service",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_prime_service_pole_line_ponderation_ServiceId",
            table: "prime_service_pole_line_ponderation",
            column: "ServiceId");

        migrationBuilder.CreateIndex(
            name: "IX_prime_service_pole_line_ponderation_ServiceId_SortOrder",
            table: "prime_service_pole_line_ponderation",
            columns: new[] { "ServiceId", "SortOrder" });

        migrationBuilder.CreateIndex(
            name: "IX_prime_service_pole_line_ponderation_ServiceId_TemplateStableId",
            table: "prime_service_pole_line_ponderation",
            columns: new[] { "ServiceId", "TemplateStableId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "prime_service_pole_line_ponderation");
    }
}
