using System;
using Formation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Formation.Infrastructure.Migrations;

[DbContext(typeof(FormationDbContext))]
[Migration("20260709180000_AddTrainingWorkflow")]
public partial class AddTrainingWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "training_sessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Description = table.Column<string>(type: "text", nullable: false),
                Type = table.Column<int>(type: "integer", nullable: false),
                AnimatorKind = table.Column<int>(type: "integer", nullable: false),
                AnimatorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                ExternalAnimatorName = table.Column<string>(type: "text", nullable: true),
                ExternalAnimatorOrganization = table.Column<string>(type: "text", nullable: true),
                ExternalAnimatorEmail = table.Column<string>(type: "text", nullable: true),
                ExternalAnimatorPhone = table.Column<string>(type: "text", nullable: true),
                PlannedStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                PlannedEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Capacity = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                CreatedByUserId = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_training_sessions", x => x.Id));

        migrationBuilder.CreateTable(
            name: "training_assignments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                EmployeeName = table.Column<string>(type: "text", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_training_assignments", x => x.Id);
                table.ForeignKey(
                    name: "FK_training_assignments_training_sessions_SessionId",
                    column: x => x.SessionId,
                    principalTable: "training_sessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "initial_training_paths",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                EmployeeName = table.Column<string>(type: "text", nullable: false),
                DateDebut = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DateFinPrevue = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                QuizScore = table.Column<decimal>(type: "numeric", nullable: true),
                QuizPassed = table.Column<bool>(type: "boolean", nullable: true),
                QuizRecordedBy = table.Column<string>(type: "text", nullable: true),
                FormateurComment = table.Column<string>(type: "text", nullable: true),
                FormateurValidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                RhValidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                RejectedBy = table.Column<string>(type: "text", nullable: true),
                RejectReason = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_initial_training_paths", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_training_assignments_SessionId_EmployeeId",
            table: "training_assignments",
            columns: new[] { "SessionId", "EmployeeId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_initial_training_paths_EmployeeId",
            table: "initial_training_paths",
            column: "EmployeeId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "training_assignments");
        migrationBuilder.DropTable(name: "initial_training_paths");
        migrationBuilder.DropTable(name: "training_sessions");
    }
}
