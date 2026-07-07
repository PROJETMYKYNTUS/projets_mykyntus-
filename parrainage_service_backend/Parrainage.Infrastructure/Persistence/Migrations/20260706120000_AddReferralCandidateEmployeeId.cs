using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Parrainage.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralCandidateEmployeeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CandidateEmployeeId",
                table: "parrainage_referral",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_parrainage_referral_CandidateEmployeeId",
                table: "parrainage_referral",
                column: "CandidateEmployeeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_parrainage_referral_CandidateEmployeeId",
                table: "parrainage_referral");

            migrationBuilder.DropColumn(
                name: "CandidateEmployeeId",
                table: "parrainage_referral");
        }
    }
}
