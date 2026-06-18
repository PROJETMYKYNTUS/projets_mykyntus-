using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PlanningService.Data;

#nullable disable

namespace PlanningService.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260612180000_AddNewsletterCoverImage")]
public partial class AddNewsletterCoverImage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CoverImageUrl",
            table: "Newsletters",
            type: "text",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CoverImageUrl",
            table: "Newsletters");
    }
}
