namespace EmployeeDirectory.Application.Dtos;

public record EmployeeDto(
    string Id,
    string FirstName,
    string LastName,
    string Role,
    string? ParentId,
    string? ServiceId,
    string PoleId,
    string? CelluleId,
    string Email,
    string? Avatar);

public record OrgOverviewDto(
    IReadOnlyList<EtageNodeDto> Etages,
    IReadOnlyList<ServiceNodeDto> Services,
    IReadOnlyList<SousServiceNodeDto> SousServices,
    IReadOnlyList<EmployeeDto> Employees,
    IReadOnlyList<DepartmentDto> Departments,
    IReadOnlyList<ManagerEtageAssignmentDto> ManagerEtage,
    IReadOnlyList<SupervisorServiceAssignmentDto> SupervisorService,
    IReadOnlyList<CoachSousServiceAssignmentDto> CoachSousService,
    IReadOnlyList<CoachPilotLinkDto> CoachPilot);

public record EtageNodeDto(string Id, string Name);
public record ServiceNodeDto(string Id, string Name, string EtageId);
public record SousServiceNodeDto(string Id, string Name, string ServiceId);
public record DepartmentDto(string Id, string Name, IReadOnlyList<PoleDto> Poles);
public record PoleDto(string Id, string Name, string PoleId, IReadOnlyList<CelluleDto> Cells);
public record CelluleDto(string Id, string Name, string CelluleId, IReadOnlyList<TeamDto> Services);
public record TeamDto(string Id, string Name, string ServiceId);

public record ManagerEtageAssignmentDto(string Id, string UserId, string EtageId);
public record SupervisorServiceAssignmentDto(string Id, string UserId, string CelluleId, string ServiceId);
public record CoachSousServiceAssignmentDto(string Id, string UserId, string ServiceId, string SousServiceId);
public record CoachPilotLinkDto(string Id, string CoachUserId, string PilotUserId);

public record CreateEmployeeRequest(
    Guid? EmployeeId,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    string? ServiceId,
    Guid? ParentId,
    DateTime? HireDate);

public record UpdateEmployeeRequest(
    string FirstName,
    string LastName,
    string Email,
    string Role,
    string? ServiceId,
    Guid? ParentId,
    bool IsActive,
    DateTime? HireDate);

public record OrgAssignmentAsOfDto(
    DateTime AsOf,
    IReadOnlyList<ActiveAssignmentDto> Assignments);

public record ActiveAssignmentDto(
    string Kind,
    string NodeId,
    string NodeLevel,
    string EmployeeId,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo);

public record AssignmentHistoryEntryDto(
    string Kind,
    string NodeId,
    string? PreviousEmployeeId,
    string? NewEmployeeId,
    DateTime ChangedAt,
    string? ChangeReason);

public record RebacSubtreeDto(string EmployeeId, IReadOnlyList<string> DescendantIds);
public record RebacManagedNodesDto(string EmployeeId, string Kind, IReadOnlyList<string> NodeIds);

public record EffectivePermissionsDto(
    string SubjectId,
    string Role,
    IReadOnlyList<string> Permissions);

public record DirectoryReconcileVerifyDto(
    int DirectoryActiveEmployees,
    int DirectoryInactiveEmployees,
    int OrgPoles,
    int OrgCellules,
    int OrgServices,
    int EmployeesWithUnmappedOrgRefs,
    int? PlanningActiveUsers,
    int? PrimeEmployeeCount,
    int? OrphansInPlanningNotDirectory,
    int? OrphansInPrimeNotDirectory,
    bool Ok);

public record DirectoryReconcileReportDto(
    int MergedDuplicates,
    int OrgNodesBackfilled,
    int EmployeesImportedFromPrime,
    int EmployeesRepublished,
    int OrphansPlanning,
    int OrphansPrime,
    int OrgGapsFixed,
    DirectoryReconcileVerifyDto Verify);
