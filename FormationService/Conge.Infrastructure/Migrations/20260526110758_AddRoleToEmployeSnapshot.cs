using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Conge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleToEmployeSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "employe_snapshots",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "employe_snapshots");
        }
    }
}
