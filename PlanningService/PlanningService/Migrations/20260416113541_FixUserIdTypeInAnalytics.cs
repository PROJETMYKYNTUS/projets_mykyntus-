using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PlanningService.Migrations
{
    /// <inheritdoc />
    public partial class FixUserIdTypeInAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CampaignAnalytics_NewsletterSubscribers_SubscriberId",
                table: "CampaignAnalytics");

            migrationBuilder.DropTable(
                name: "NewsletterSubscribers");

            migrationBuilder.DropIndex(
                name: "IX_CampaignAnalytics_SubscriberId",
                table: "CampaignAnalytics");

            migrationBuilder.DropColumn(
                name: "SubscriberId",
                table: "CampaignAnalytics");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "CampaignAnalytics",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "CampaignAnalytics");

            migrationBuilder.AddColumn<int>(
                name: "SubscriberId",
                table: "CampaignAnalytics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "NewsletterSubscribers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Group = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SubscribedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UnsubscribedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsletterSubscribers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAnalytics_SubscriberId",
                table: "CampaignAnalytics",
                column: "SubscriberId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterSubscribers_Email",
                table: "NewsletterSubscribers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterSubscribers_UserId",
                table: "NewsletterSubscribers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignAnalytics_NewsletterSubscribers_SubscriberId",
                table: "CampaignAnalytics",
                column: "SubscriberId",
                principalTable: "NewsletterSubscribers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
