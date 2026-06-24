using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Parrainage.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralRuleDurationAndAppliedRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinDurationMonths",
                table: "parrainage_referral_rule",
                type: "integer",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddColumn<string>(
                name: "AppliedRuleId",
                table: "parrainage_referral",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PositionMode",
                table: "parrainage_referral",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "CUSTOM");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinDurationMonths",
                table: "parrainage_referral_rule");

            migrationBuilder.DropColumn(
                name: "AppliedRuleId",
                table: "parrainage_referral");

            migrationBuilder.DropColumn(
                name: "PositionMode",
                table: "parrainage_referral");
        }
    }
}
