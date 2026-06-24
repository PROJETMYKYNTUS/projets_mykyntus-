using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Parrainage.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialParrainage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "parrainage_audit_log",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Action = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UserLabel = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Details = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parrainage_audit_log", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "parrainage_notification_preference",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Email = table.Column<bool>(type: "boolean", nullable: false),
                    InApp = table.Column<bool>(type: "boolean", nullable: false),
                    SystemAlerts = table.Column<bool>(type: "boolean", nullable: false),
                    Referrals = table.Column<bool>(type: "boolean", nullable: false),
                    Approvals = table.Column<bool>(type: "boolean", nullable: false),
                    Payments = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parrainage_notification_preference", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "parrainage_referral",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReferrerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReferrerName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProjectId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProjectName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TeamId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CandidateName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CandidateEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CandidatePhone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Position = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RewardAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CvUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parrainage_referral", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "parrainage_referral_history",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ReferralId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CandidateName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PerformedById = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PerformedByLabel = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    RewardAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parrainage_referral_history", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "parrainage_referral_notification",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Message = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Read = table.Column<bool>(type: "boolean", nullable: false),
                    ReferralId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ReferrerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TargetRoles = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parrainage_referral_notification", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "parrainage_referral_rule",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Target = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parrainage_referral_rule", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "parrainage_system_config",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    DefaultBonusAmount = table.Column<int>(type: "integer", nullable: false),
                    MinDurationMonths = table.Column<int>(type: "integer", nullable: false),
                    ReferralLimitPerEmployee = table.Column<int>(type: "integer", nullable: false),
                    PendingReferralAlertThreshold = table.Column<int>(type: "integer", nullable: true),
                    ReferralProgramRules = table.Column<string>(type: "jsonb", nullable: true),
                    AdminWorkflow = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parrainage_system_config", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_parrainage_audit_log_Timestamp",
                table: "parrainage_audit_log",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_parrainage_referral_CandidateEmail",
                table: "parrainage_referral",
                column: "CandidateEmail");

            migrationBuilder.CreateIndex(
                name: "IX_parrainage_referral_ReferrerId",
                table: "parrainage_referral",
                column: "ReferrerId");

            migrationBuilder.CreateIndex(
                name: "IX_parrainage_referral_Status",
                table: "parrainage_referral",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_parrainage_referral_history_CreatedAt",
                table: "parrainage_referral_history",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_parrainage_referral_history_ReferralId",
                table: "parrainage_referral_history",
                column: "ReferralId");

            migrationBuilder.CreateIndex(
                name: "IX_parrainage_referral_notification_CreatedAt",
                table: "parrainage_referral_notification",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parrainage_audit_log");

            migrationBuilder.DropTable(
                name: "parrainage_notification_preference");

            migrationBuilder.DropTable(
                name: "parrainage_referral");

            migrationBuilder.DropTable(
                name: "parrainage_referral_history");

            migrationBuilder.DropTable(
                name: "parrainage_referral_notification");

            migrationBuilder.DropTable(
                name: "parrainage_referral_rule");

            migrationBuilder.DropTable(
                name: "parrainage_system_config");
        }
    }
}
