using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrimeBackend.Data.Migrations
{
    /// <inheritdoc />
    public partial class TemplateCalcSnapshotOnPoleDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TemplateCalcSnapshotJson",
                table: "prime_supervisor_pole_prime_draft",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TemplateCalcSnapshotJson",
                table: "prime_supervisor_pole_prime_draft");
        }
    }
}
