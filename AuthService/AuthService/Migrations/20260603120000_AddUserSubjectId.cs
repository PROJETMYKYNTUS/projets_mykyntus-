using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthService.Migrations;

public partial class AddUserSubjectId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "SubjectId",
            table: "Users",
            type: "uuid",
            nullable: false,
            defaultValue: Guid.Empty);

        migrationBuilder.CreateIndex(
            name: "IX_Users_SubjectId",
            table: "Users",
            column: "SubjectId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Users_SubjectId", table: "Users");
        migrationBuilder.DropColumn(name: "SubjectId", table: "Users");
    }
}
