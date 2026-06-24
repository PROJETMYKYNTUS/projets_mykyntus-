using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Planning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeCustomFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "EmployeeImportFieldConfigs",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "DataType",
                table: "EmployeeImportFieldConfigs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "text");

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemField",
                table: "EmployeeImportFieldConfigs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "UserCustomFieldValues",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCustomFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCustomFieldValues_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCustomFieldValues_UserId_FieldKey",
                table: "UserCustomFieldValues",
                columns: new[] { "UserId", "FieldKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserCustomFieldValues");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "EmployeeImportFieldConfigs");

            migrationBuilder.DropColumn(
                name: "DataType",
                table: "EmployeeImportFieldConfigs");

            migrationBuilder.DropColumn(
                name: "IsSystemField",
                table: "EmployeeImportFieldConfigs");
        }
    }
}
