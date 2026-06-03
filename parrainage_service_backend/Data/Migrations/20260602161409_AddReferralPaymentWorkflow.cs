using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParrainageBackend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralPaymentWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAt",
                table: "parrainage_referral",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CandidateStartDate",
                table: "parrainage_referral",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EligibilityNotifiedAt",
                table: "parrainage_referral",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EligibleForPaymentAt",
                table: "parrainage_referral",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaidAt",
                table: "parrainage_referral",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaidByLabel",
                table: "parrainage_referral",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaidByUserId",
                table: "parrainage_referral",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                table: "parrainage_referral",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "parrainage_referral",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "NOT_ELIGIBLE");

            migrationBuilder.CreateIndex(
                name: "IX_parrainage_referral_PaymentStatus",
                table: "parrainage_referral",
                column: "PaymentStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_parrainage_referral_PaymentStatus",
                table: "parrainage_referral");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "parrainage_referral");

            migrationBuilder.DropColumn(
                name: "CandidateStartDate",
                table: "parrainage_referral");

            migrationBuilder.DropColumn(
                name: "EligibilityNotifiedAt",
                table: "parrainage_referral");

            migrationBuilder.DropColumn(
                name: "EligibleForPaymentAt",
                table: "parrainage_referral");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "parrainage_referral");

            migrationBuilder.DropColumn(
                name: "PaidByLabel",
                table: "parrainage_referral");

            migrationBuilder.DropColumn(
                name: "PaidByUserId",
                table: "parrainage_referral");

            migrationBuilder.DropColumn(
                name: "PaymentReference",
                table: "parrainage_referral");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "parrainage_referral");
        }
    }
}
