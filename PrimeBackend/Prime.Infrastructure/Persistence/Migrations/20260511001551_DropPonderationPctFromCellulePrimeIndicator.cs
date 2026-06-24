using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropPonderationPctFromCellulePrimeIndicator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PonderationPct",
                table: "prime_cellule_prime_indicator");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PonderationPct",
                table: "prime_cellule_prime_indicator",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);
        }
    }
}
