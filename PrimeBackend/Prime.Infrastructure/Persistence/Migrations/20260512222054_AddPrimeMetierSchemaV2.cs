using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrimeMetierSchemaV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_prime_employee_prime_team_TeamId",
                table: "prime_employee");

            migrationBuilder.DropForeignKey(
                name: "FK_prime_pole_prime_department_DepartmentId",
                table: "prime_pole");

            migrationBuilder.DropTable(
                name: "prime_cellule_prime_indicator");

            migrationBuilder.DropTable(
                name: "prime_department");

            migrationBuilder.DropTable(
                name: "prime_employee_prime_cell_fiche");

            migrationBuilder.DropTable(
                name: "prime_team");

            migrationBuilder.DropTable(
                name: "prime_supervisor_pole_prime_draft");

            migrationBuilder.DropIndex(
                name: "IX_prime_pole_DepartmentId",
                table: "prime_pole");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "prime_pole");

            migrationBuilder.DropColumn(
                name: "DepartementId",
                table: "prime_employee");

            migrationBuilder.RenameColumn(
                name: "PoleId",
                table: "prime_supervisor_fiche",
                newName: "CelluleId");

            migrationBuilder.RenameColumn(
                name: "TeamId",
                table: "prime_employee",
                newName: "ServiceId");

            migrationBuilder.RenameIndex(
                name: "IX_prime_employee_TeamId",
                table: "prime_employee",
                newName: "IX_prime_employee_ServiceId");

            migrationBuilder.CreateTable(
                name: "prime_anomaly",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DetectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    TargetEntityType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TargetEntityId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Period = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ServiceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CelluleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PoleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ContextJson = table.Column<string>(type: "text", nullable: true),
                    ResolvedByUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolutionNote = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_anomaly", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prime_audit_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UserDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DetailJson = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_audit_log", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prime_rbac_permission",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Scope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_rbac_permission", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prime_service",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CelluleId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_service", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prime_service_prime_cellule_CelluleId",
                        column: x => x.CelluleId,
                        principalTable: "prime_cellule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prime_supervisor_cellule_prime_draft",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupervisorUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CelluleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Period = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TemplateId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TemplateDisplayName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    TemplateFormatVersion = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SchemaJson = table.Column<string>(type: "text", nullable: false),
                    CelluleSaisieJson = table.Column<string>(type: "text", nullable: false),
                    ComputedJson = table.Column<string>(type: "text", nullable: true),
                    TemplateCalcSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_supervisor_cellule_prime_draft", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prime_workflow_global_config",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    GlobalSlaHours = table.Column<int>(type: "integer", nullable: false),
                    AllowBulkApprove = table.Column<bool>(type: "boolean", nullable: false),
                    RequireRejectReason = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_workflow_global_config", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prime_workflow_step",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ApproverRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SlaHours = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_workflow_step", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prime_service_prime_indicator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    PonderationPrimePct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    PonderationChallengePct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TemplateStableId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_service_prime_indicator", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prime_service_prime_indicator_prime_service_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "prime_service",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prime_employee_prime_service_fiche",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CellulePrimeDraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupervisorUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EmployeeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ServiceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CelluleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Period = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ServiceSaisieJson = table.Column<string>(type: "text", nullable: false),
                    FillingStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidationStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastApproverUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectedByUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RejectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    PrimeAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    ChallengeAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_employee_prime_service_fiche", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prime_employee_prime_service_fiche_prime_supervisor_cellule~",
                        column: x => x.CellulePrimeDraftId,
                        principalTable: "prime_supervisor_cellule_prime_draft",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prime_anomaly_Period_ServiceId",
                table: "prime_anomaly",
                columns: new[] { "Period", "ServiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_prime_anomaly_Status",
                table: "prime_anomaly",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_prime_anomaly_TargetEntityType_TargetEntityId",
                table: "prime_anomaly",
                columns: new[] { "TargetEntityType", "TargetEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_prime_anomaly_Type",
                table: "prime_anomaly",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_prime_audit_log_At",
                table: "prime_audit_log",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_prime_audit_log_EntityType_EntityId",
                table: "prime_audit_log",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_prime_audit_log_UserId",
                table: "prime_audit_log",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_prime_employee_prime_service_fiche_CellulePrimeDraftId",
                table: "prime_employee_prime_service_fiche",
                column: "CellulePrimeDraftId");

            migrationBuilder.CreateIndex(
                name: "IX_prime_employee_prime_service_fiche_EmployeeId_Period",
                table: "prime_employee_prime_service_fiche",
                columns: new[] { "EmployeeId", "Period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prime_employee_prime_service_fiche_ServiceId_Period",
                table: "prime_employee_prime_service_fiche",
                columns: new[] { "ServiceId", "Period" });

            migrationBuilder.CreateIndex(
                name: "IX_prime_employee_prime_service_fiche_SupervisorUserId_Period",
                table: "prime_employee_prime_service_fiche",
                columns: new[] { "SupervisorUserId", "Period" });

            migrationBuilder.CreateIndex(
                name: "IX_prime_employee_prime_service_fiche_ValidationStatus",
                table: "prime_employee_prime_service_fiche",
                column: "ValidationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_prime_rbac_permission_Role_Action_Scope",
                table: "prime_rbac_permission",
                columns: new[] { "Role", "Action", "Scope" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prime_service_CelluleId",
                table: "prime_service",
                column: "CelluleId");

            migrationBuilder.CreateIndex(
                name: "IX_prime_service_prime_indicator_ServiceId",
                table: "prime_service_prime_indicator",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_prime_service_prime_indicator_ServiceId_SortOrder",
                table: "prime_service_prime_indicator",
                columns: new[] { "ServiceId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_prime_supervisor_cellule_prime_draft_SupervisorUserId_Cellu~",
                table: "prime_supervisor_cellule_prime_draft",
                columns: new[] { "SupervisorUserId", "CelluleId", "Period", "TemplateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prime_supervisor_cellule_prime_draft_SupervisorUserId_Period",
                table: "prime_supervisor_cellule_prime_draft",
                columns: new[] { "SupervisorUserId", "Period" });

            migrationBuilder.CreateIndex(
                name: "IX_prime_workflow_step_FromStatus_ToStatus",
                table: "prime_workflow_step",
                columns: new[] { "FromStatus", "ToStatus" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prime_workflow_step_SortOrder",
                table: "prime_workflow_step",
                column: "SortOrder");

            migrationBuilder.AddForeignKey(
                name: "FK_prime_employee_prime_service_ServiceId",
                table: "prime_employee",
                column: "ServiceId",
                principalTable: "prime_service",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_prime_employee_prime_service_ServiceId",
                table: "prime_employee");

            migrationBuilder.DropTable(
                name: "prime_anomaly");

            migrationBuilder.DropTable(
                name: "prime_audit_log");

            migrationBuilder.DropTable(
                name: "prime_employee_prime_service_fiche");

            migrationBuilder.DropTable(
                name: "prime_rbac_permission");

            migrationBuilder.DropTable(
                name: "prime_service_prime_indicator");

            migrationBuilder.DropTable(
                name: "prime_workflow_global_config");

            migrationBuilder.DropTable(
                name: "prime_workflow_step");

            migrationBuilder.DropTable(
                name: "prime_supervisor_cellule_prime_draft");

            migrationBuilder.DropTable(
                name: "prime_service");

            migrationBuilder.RenameColumn(
                name: "CelluleId",
                table: "prime_supervisor_fiche",
                newName: "PoleId");

            migrationBuilder.RenameColumn(
                name: "ServiceId",
                table: "prime_employee",
                newName: "TeamId");

            migrationBuilder.RenameIndex(
                name: "IX_prime_employee_ServiceId",
                table: "prime_employee",
                newName: "IX_prime_employee_TeamId");

            migrationBuilder.AddColumn<string>(
                name: "DepartmentId",
                table: "prime_pole",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DepartementId",
                table: "prime_employee",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "prime_cellule_prime_indicator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CelluleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Label = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    PonderationChallengePct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    PonderationPrimePct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TemplateStableId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_cellule_prime_indicator", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prime_cellule_prime_indicator_prime_cellule_CelluleId",
                        column: x => x.CelluleId,
                        principalTable: "prime_cellule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prime_department",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_department", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prime_supervisor_pole_prime_draft",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComputedJson = table.Column<string>(type: "text", nullable: true),
                    Period = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PoleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PoleSaisieJson = table.Column<string>(type: "text", nullable: false),
                    SchemaJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SupervisorUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TemplateCalcSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    TemplateDisplayName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    TemplateFormatVersion = table.Column<int>(type: "integer", nullable: false),
                    TemplateId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_supervisor_pole_prime_draft", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prime_team",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CelluleId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_team", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prime_team_prime_cellule_CelluleId",
                        column: x => x.CelluleId,
                        principalTable: "prime_cellule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prime_employee_prime_cell_fiche",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PolePrimeDraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    CellSaisieJson = table.Column<string>(type: "text", nullable: false),
                    CelluleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EmployeeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FillingStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Period = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PoleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SupervisorUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prime_employee_prime_cell_fiche", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prime_employee_prime_cell_fiche_prime_supervisor_pole_prime~",
                        column: x => x.PolePrimeDraftId,
                        principalTable: "prime_supervisor_pole_prime_draft",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prime_pole_DepartmentId",
                table: "prime_pole",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_prime_cellule_prime_indicator_CelluleId",
                table: "prime_cellule_prime_indicator",
                column: "CelluleId");

            migrationBuilder.CreateIndex(
                name: "IX_prime_cellule_prime_indicator_CelluleId_SortOrder",
                table: "prime_cellule_prime_indicator",
                columns: new[] { "CelluleId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_prime_employee_prime_cell_fiche_CelluleId_Period",
                table: "prime_employee_prime_cell_fiche",
                columns: new[] { "CelluleId", "Period" });

            migrationBuilder.CreateIndex(
                name: "IX_prime_employee_prime_cell_fiche_EmployeeId_Period",
                table: "prime_employee_prime_cell_fiche",
                columns: new[] { "EmployeeId", "Period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prime_employee_prime_cell_fiche_PolePrimeDraftId",
                table: "prime_employee_prime_cell_fiche",
                column: "PolePrimeDraftId");

            migrationBuilder.CreateIndex(
                name: "IX_prime_employee_prime_cell_fiche_SupervisorUserId_Period",
                table: "prime_employee_prime_cell_fiche",
                columns: new[] { "SupervisorUserId", "Period" });

            migrationBuilder.CreateIndex(
                name: "IX_prime_supervisor_pole_prime_draft_SupervisorUserId_Period",
                table: "prime_supervisor_pole_prime_draft",
                columns: new[] { "SupervisorUserId", "Period" });

            migrationBuilder.CreateIndex(
                name: "IX_prime_supervisor_pole_prime_draft_SupervisorUserId_PoleId_P~",
                table: "prime_supervisor_pole_prime_draft",
                columns: new[] { "SupervisorUserId", "PoleId", "Period", "TemplateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prime_team_CelluleId",
                table: "prime_team",
                column: "CelluleId");

            migrationBuilder.AddForeignKey(
                name: "FK_prime_employee_prime_team_TeamId",
                table: "prime_employee",
                column: "TeamId",
                principalTable: "prime_team",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_prime_pole_prime_department_DepartmentId",
                table: "prime_pole",
                column: "DepartmentId",
                principalTable: "prime_department",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
