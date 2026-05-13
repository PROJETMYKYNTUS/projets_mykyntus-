namespace PrimeBackend.Models;

public class Department
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<Pole> Poles { get; set; } = [];
}

public class Pole
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string DepartmentId { get; set; } = "";
    public List<Cellule> Cells { get; set; } = [];
}

public class Cellule
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string PoleId { get; set; } = "";
    public List<Team> Teams { get; set; } = [];
}

public class Team
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string CelluleId { get; set; } = "";
}

// -----------------------------
// Models (Org assignments)
// -----------------------------
public class EtageNode
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public class ServiceNode
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string EtageId { get; set; } = "";
}

public class SousServiceNode
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ServiceId { get; set; } = "";
}

public class ManagerEtageAssignment
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string EtageId { get; set; } = "";
}

public class SupervisorServiceAssignment
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string ServiceId { get; set; } = "";
}

public class CoachSousServiceAssignment
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string SousServiceId { get; set; } = "";
}

public class CoachPilotLink
{
    public string Id { get; set; } = "";
    public string CoachUserId { get; set; } = "";
    public string PilotUserId { get; set; } = "";
}

public class Employee
{
    public string Id { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    // Role values are kept as string to avoid breaking JSON contract.
    public string Role { get; set; } = "";
    /// <summary>Supérieur hiérarchique ; autorité descendante : RP → Manager → Superviseur → Coach → Pilote (Pilote = employé en rôle Pilote).</summary>
    public string? ParentId { get; set; }
    public string TeamId { get; set; } = "";
    /// <summary>Département → Pôle → Cellule (aligné sur la structure organisationnelle).</summary>
    public string DepartementId { get; set; } = "";
    public string PoleId { get; set; } = "";
    public string CelluleId { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Avatar { get; set; }
}

public class PrimeType
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string DepartmentId { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Description { get; set; }
}

public class PrimeRule
{
    public string Id { get; set; } = "";
    public string PrimeTypeId { get; set; } = "";
    public string? DepartmentId { get; set; }
    public string? PoleId { get; set; }
    public string? CelluleId { get; set; }
    public string? TeamId { get; set; }
    public string? RoleId { get; set; }
    public string ConditionField { get; set; } = "";
    public string ConditionType { get; set; } = "";
    public int TargetValue { get; set; }
    public string CalculationMethod { get; set; } = "";
    public int Amount { get; set; }
    public string Period { get; set; } = "";
}

public class PrimeResult
{
    public string Id { get; set; } = "";
    public string EmployeeId { get; set; } = "";
    public string PrimeTypeId { get; set; } = "";
    public int Score { get; set; }
    public int Amount { get; set; }
    public string Status { get; set; } = "";
    public string Period { get; set; } = "";
    public string? ApprovedBy { get; set; }
    public string Date { get; set; } = "";
}

// -----------------------------
// Models (RP)
// -----------------------------

public class RpTeamMemberPerformance
{
    public string EmployeeId { get; set; } = "";
    public string EmployeeName { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public int CompletedTasks { get; set; }
    public int TotalTasks { get; set; }
    public int ObjectivesReached { get; set; }
    public int TotalObjectives { get; set; }
    public List<MonthlyPerformancePoint> MonthlyPerformance { get; set; } = [];
}

public class MonthlyPerformancePoint
{
    public string Month { get; set; } = "";
    public int Score { get; set; }
}

public class RpValidationItem
{
    public string Id { get; set; } = "";
    public string EmployeeId { get; set; } = "";
    public string EmployeeName { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public int PerformanceScore { get; set; }
    public bool ManagerValidated { get; set; }
    public string Status { get; set; } = "";
    public string Period { get; set; } = "";
}

public class RpDashboardStats
{
    public int ProjectProgress { get; set; }
    public int CompletedTasks { get; set; }
    public int AverageTeamPerformance { get; set; }
    public int PendingValidations { get; set; }
    public List<MonthScore> PerformanceEvolution { get; set; } = [];
    public List<MemberPerformance> MemberPerformance { get; set; } = [];
}

public class MonthScore
{
    public string Month { get; set; } = "";
    public int Score { get; set; }
}

public class MemberPerformance
{
    public string Name { get; set; } = "";
    public int Score { get; set; }
    public string Status { get; set; } = "";
}

// -----------------------------
// Models (Admin)
// -----------------------------

public class AdminSystemKpi
{
    public int TotalGeneratedPrimes { get; set; }
    public int ValidationsInProgress { get; set; }
    public int ErrorCount { get; set; }
    public int AvgProcessingTimeSec { get; set; }
}

public class AdminSystemAlert
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Date { get; set; } = "";
}

public class AdminCalculationConfig
{
    public string Formula { get; set; } = "";
    public AdminCalculationWeights Weights { get; set; } = new();
    public AdminCalculationParameters Parameters { get; set; } = new();
}

public class AdminCalculationWeights
{
    public int IndividualPerformance { get; set; }
    public int TeamPerformance { get; set; }
    public int Objectives { get; set; }
}

public class AdminCalculationParameters
{
    public int Cap { get; set; }
    public int MinThreshold { get; set; }
    public int Bonus { get; set; }
}

public class AdminAuditLog
{
    public string Id { get; set; } = "";
    public string User { get; set; } = "";
    public string Action { get; set; } = "";
    public string Date { get; set; } = "";
}

public class AdminAnomaly
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "";
}

public class AdminChartPoint
{
    public string Month { get; set; } = "";
    public int Value { get; set; }
}

public class AdminByDepartmentPoint
{
    public string Name { get; set; } = "";
    public int Value { get; set; }
}

public class AdminDashboardCharts
{
    public List<AdminChartPoint> VolumeByMonth { get; set; } = [];
    public List<AdminChartPoint> ValidationRate { get; set; } = [];
    public List<AdminByDepartmentPoint> ByDepartment { get; set; } = [];
}

public class AdminDashboardResponse
{
    public AdminSystemKpi Kpis { get; set; } = new();
    public AdminDashboardCharts Charts { get; set; } = new();
    public List<AdminSystemAlert> Alerts { get; set; } = [];
}

public class AdminWorkflowConfig
{
    public List<string> Steps { get; set; } = [];
    public int SlaHours { get; set; }
    public bool NotificationsEnabled { get; set; }
}

public class AdminRbacRow
{
    public string Role { get; set; } = "";
    public bool Read { get; set; }
    public bool Edit { get; set; }
    public bool Validate { get; set; }
    public bool Configure { get; set; }
}

// -----------------------------
// Models (Audit)
// -----------------------------

public class AuditValidationStep
{
    public string Role { get; set; } = "";
    public string Status { get; set; } = "";
    public string Date { get; set; } = "";
}

public class AuditOperation
{
    public string Id { get; set; } = "";
    public string EmployeeName { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public List<AuditValidationStep> Steps { get; set; } = [];
    public string ValidatedBy { get; set; } = "";
    public string Date { get; set; } = "";
    public string Status { get; set; } = "";
}

public class AuditTrailLog
{
    public string Id { get; set; } = "";
    public string User { get; set; } = "";
    public string Action { get; set; } = "";
    public string Date { get; set; } = "";
    public string Detail { get; set; } = "";
}

public class AuditAnomaly
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public string? ValidationId { get; set; }
    public string Status { get; set; } = "";
}

public class AuditKpis
{
    public int TotalPrimes { get; set; }
    public int Validations { get; set; }
    public int Anomalies { get; set; }
    public int ConformityRate { get; set; }
}

public class AuditFlowByStepPoint
{
    public string Step { get; set; } = "";
    public int Value { get; set; }
}

public class AuditNamedPoint
{
    public string Name { get; set; } = "";
    public int Value { get; set; }
}

public class AuditActivityByRolePoint
{
    public string Role { get; set; } = "";
    public int Value { get; set; }
}

public class AuditDashboardCharts
{
    public List<AuditFlowByStepPoint> FlowByStep { get; set; } = [];
    public List<AuditNamedPoint> ValidationVsRejection { get; set; } = [];
    public List<AuditActivityByRolePoint> ActivityByRole { get; set; } = [];
}

public class AuditDashboardResponse
{
    public AuditKpis Kpis { get; set; } = new();
    public AuditDashboardCharts Charts { get; set; } = new();
}
public class PrimeConfigItem
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Sector { get; set; } = "";
    public string GroupCode { get; set; } = "";
    public string ActivityType { get; set; } = "";
    public string? Label { get; set; }
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
    public bool InvertedLogic { get; set; }
    public decimal? Weight { get; set; }
    public decimal? PrimeCap { get; set; }
    public decimal? ChallengeCap { get; set; }
}
