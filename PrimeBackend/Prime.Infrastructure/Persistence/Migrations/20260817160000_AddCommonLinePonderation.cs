using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations;

[Migration("20260817160000_AddCommonLinePonderation")]
public partial class AddCommonLinePonderation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "prime_common_line_ponderation",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ScopeType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                ScopeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                TemplateId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                TemplateStableId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Label = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                Contract = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                PonderationPrimePct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                PonderationChallengePct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_prime_common_line_ponderation", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_prime_common_line_ponderation_scope_template_from",
            table: "prime_common_line_ponderation",
            columns: new[] { "ScopeType", "ScopeId", "TemplateId", "TemplateStableId", "EffectiveFrom" });

        migrationBuilder.CreateIndex(
            name: "IX_prime_common_line_ponderation_scope_stable_to",
            table: "prime_common_line_ponderation",
            columns: new[] { "ScopeType", "ScopeId", "TemplateStableId", "EffectiveTo" });

        migrationBuilder.AddColumn<string>(
            name: "PonderationsSnapshotJson",
            table: "prime_employee_prime_service_fiche",
            type: "text",
            nullable: true);

        migrationBuilder.Sql(
            """
            INSERT INTO prime_common_line_ponderation (
                "Id", "ScopeType", "ScopeId", "TemplateId", "TemplateStableId",
                "Label", "Contract", "SortOrder",
                "PonderationPrimePct", "PonderationChallengePct",
                "EffectiveFrom", "EffectiveTo", "CreatedBy", "CreatedAt")
            SELECT
                gen_random_uuid(),
                'Service',
                p."ServiceId",
                '',
                p."TemplateStableId",
                COALESCE(p."Label", ''),
                '',
                p."SortOrder",
                p."PonderationPrimePct",
                p."PonderationChallengePct",
                TIMESTAMPTZ '2000-01-01 00:00:00+00',
                NULL,
                NULL,
                COALESCE(p."CreatedAt", NOW())
            FROM prime_service_pole_line_ponderation p
            WHERE NOT EXISTS (
                SELECT 1 FROM prime_common_line_ponderation c
                WHERE c."ScopeType" = 'Service'
                  AND c."ScopeId" = p."ServiceId"
                  AND c."TemplateStableId" = p."TemplateStableId"
                  AND c."EffectiveTo" IS NULL);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PonderationsSnapshotJson",
            table: "prime_employee_prime_service_fiche");
        migrationBuilder.DropTable(name: "prime_common_line_ponderation");
    }
}
