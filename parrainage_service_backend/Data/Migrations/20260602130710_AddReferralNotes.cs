using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParrainageBackend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "parrainage_referral",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "parrainage_referral");
        }
    }
}
