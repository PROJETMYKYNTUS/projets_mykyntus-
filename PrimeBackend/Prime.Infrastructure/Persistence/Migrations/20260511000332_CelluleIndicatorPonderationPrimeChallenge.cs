using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CelluleIndicatorPonderationPrimeChallenge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PonderationPrimePct",
                table: "prime_cellule_prime_indicator",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PonderationChallengePct",
                table: "prime_cellule_prime_indicator",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PonderationPrimePct",
                table: "prime_cellule_prime_indicator");

            migrationBuilder.DropColumn(
                name: "PonderationChallengePct",
                table: "prime_cellule_prime_indicator");
        }
    }
}
