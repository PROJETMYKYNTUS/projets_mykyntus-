using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Parrainage.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralTrainingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "TrainingEndDate",
                table: "parrainage_referral",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ProductionStartDate",
                table: "parrainage_referral",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TrainingEndNotifiedAt",
                table: "parrainage_referral",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrainingEndDate",
                table: "parrainage_referral");

            migrationBuilder.DropColumn(
                name: "ProductionStartDate",
                table: "parrainage_referral");

            migrationBuilder.DropColumn(
                name: "TrainingEndNotifiedAt",
                table: "parrainage_referral");
        }
    }
}
