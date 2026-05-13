using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrimeBackend.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialPrimeOrg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "prime_department",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_department", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prime_pole",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DepartmentId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_pole", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prime_pole_prime_department_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "prime_department",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prime_cellule",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PoleId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_cellule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prime_cellule_prime_pole_PoleId",
                        column: x => x.PoleId,
                        principalTable: "prime_pole",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prime_team",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CelluleId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_team", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prime_team_prime_cellule_CelluleId",
                        column: x => x.CelluleId,
                        principalTable: "prime_cellule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prime_employee",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LastName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ParentId = table.Column<string>(type: "text", nullable: true),
                    TeamId = table.Column<string>(type: "text", nullable: false),
                    DepartementId = table.Column<string>(type: "text", nullable: false),
                    PoleId = table.Column<string>(type: "text", nullable: false),
                    CelluleId = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Avatar = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_employee", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prime_employee_prime_team_TeamId",
                        column: x => x.TeamId,
                        principalTable: "prime_team",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prime_cellule_PoleId",
                table: "prime_cellule",
                column: "PoleId");

            migrationBuilder.CreateIndex(
                name: "IX_prime_employee_PoleId",
                table: "prime_employee",
                column: "PoleId");

            migrationBuilder.CreateIndex(
                name: "IX_prime_employee_TeamId",
                table: "prime_employee",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_prime_pole_DepartmentId",
                table: "prime_pole",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_prime_team_CelluleId",
                table: "prime_team",
                column: "CelluleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prime_employee");

            migrationBuilder.DropTable(
                name: "prime_team");

            migrationBuilder.DropTable(
                name: "prime_cellule");

            migrationBuilder.DropTable(
                name: "prime_pole");

            migrationBuilder.DropTable(
                name: "prime_department");
        }
    }
}
