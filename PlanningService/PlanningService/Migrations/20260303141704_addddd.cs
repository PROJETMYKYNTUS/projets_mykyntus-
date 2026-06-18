using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanningService.Migrations
{
    /// <inheritdoc />
    public partial class addddd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsNewEmployee",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsNewEmployee",
                table: "Users");
        }
    }
}
