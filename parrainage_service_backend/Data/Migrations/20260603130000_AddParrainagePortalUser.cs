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
                Id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                ProjectId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                ParentId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_parrainage_portal_user", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_parrainage_portal_user_Email",
            table: "parrainage_portal_user",
            column: "Email",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "parrainage_portal_user");
    }
}
