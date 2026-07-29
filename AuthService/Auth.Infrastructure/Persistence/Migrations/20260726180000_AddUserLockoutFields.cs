using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.Infrastructure.Persistence.Migrations;

[DbContext(typeof(Auth.Infrastructure.Persistence.AuthDbContext))]
[Migration("20260726180000_AddUserLockoutFields")]
public partial class AddUserLockoutFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AccessFailedCount",
            table: "Users",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "LockoutEnd",
            table: "Users",
            type: "timestamp with time zone",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AccessFailedCount", table: "Users");
        migrationBuilder.DropColumn(name: "LockoutEnd", table: "Users");
    }
}
