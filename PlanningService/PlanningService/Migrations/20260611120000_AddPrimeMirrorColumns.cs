using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PlanningService.Data;

#nullable disable

namespace PlanningService.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260611120000_AddPrimeMirrorColumns")]
public partial class AddPrimeMirrorColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PrimePoleId",
            table: "Floors",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PrimeCelluleId",
            table: "Services",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PrimeServiceId",
            table: "SubServices",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Floors_PrimePoleId",
            table: "Floors",
            column: "PrimePoleId",
            unique: true,
            filter: "\"PrimePoleId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Services_PrimeCelluleId",
            table: "Services",
            column: "PrimeCelluleId",
            unique: true,
            filter: "\"PrimeCelluleId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_SubServices_PrimeServiceId",
            table: "SubServices",
            column: "PrimeServiceId",
            unique: true,
            filter: "\"PrimeServiceId\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_SubServices_PrimeServiceId", table: "SubServices");
        migrationBuilder.DropIndex(name: "IX_Services_PrimeCelluleId", table: "Services");
        migrationBuilder.DropIndex(name: "IX_Floors_PrimePoleId", table: "Floors");

        migrationBuilder.DropColumn(name: "PrimeServiceId", table: "SubServices");
        migrationBuilder.DropColumn(name: "PrimeCelluleId", table: "Services");
        migrationBuilder.DropColumn(name: "PrimePoleId", table: "Floors");
    }
}
