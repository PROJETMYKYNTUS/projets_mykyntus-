using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlanningService.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSubServiceTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserSubServices",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    SubServiceId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSubServices", x => new { x.UserId, x.SubServiceId });
                    table.ForeignKey(
                        name: "FK_UserSubServices_SubServices_SubServiceId",
                        column: x => x.SubServiceId,
                        principalTable: "SubServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserSubServices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSubServices_SubServiceId",
                table: "UserSubServices",
                column: "SubServiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserSubServices");
        }
    }
}
