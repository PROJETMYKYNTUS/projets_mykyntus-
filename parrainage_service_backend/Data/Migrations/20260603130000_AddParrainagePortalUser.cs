using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParrainageBackend.Data.Migrations;

public partial class AddParrainagePortalUser : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "parrainage_portal_user",
            columns: table => new
            {
                id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                project_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                parent_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_parrainage_portal_user", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_parrainage_portal_user_email",
            table: "parrainage_portal_user",
            column: "email",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "parrainage_portal_user");
    }
}
