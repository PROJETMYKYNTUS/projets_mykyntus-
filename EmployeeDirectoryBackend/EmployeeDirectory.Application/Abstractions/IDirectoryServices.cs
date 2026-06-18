using EmployeeDirectory.Application.Dtos;
using Kyntus.Messaging.Contracts;

namespace EmployeeDirectory.Application.Abstractions;

public interface IDirectoryReadService
{
    Task<IReadOnlyList<EmployeeDto>> GetEmployeesAsync(string? role, string? poleId, CancellationToken ct = default);
    Task<EmployeeDto?> GetEmployeeByIdAsync(Guid id, CancellationToken ct = default);
    Task<OrgOverviewDto> GetOrgOverviewAsync(CancellationToken ct = default);
    Task<OrgAssignmentAsOfDto> GetAssignmentsAsOfAsync(DateTime asOf, CancellationToken ct = default);
    Task<IReadOnlyList<AssignmentHistoryEntryDto>> GetAssignmentHistoryAsync(Guid employeeId, CancellationToken ct = default);
    Task<bool> IsDescendantAsync(Guid viewerId, Guid targetId, CancellationToken ct = default);
    Task<RebacSubtreeDto> GetSubtreeAsync(Guid employeeId, CancellationToken ct = default);
    Task<RebacManagedNodesDto> GetManagedNodesAsync(Guid employeeId, string kind, CancellationToken ct = default);
}

public interface IDirectoryWriteService
{
    Task<EmployeeDto> CreateEmployeeAsync(CreateEmployeeRequest request, Guid? changedBy, CancellationToken ct = default);
    Task<EmployeeDto?> UpdateEmployeeAsync(Guid id, UpdateEmployeeRequest request, Guid? changedBy, CancellationToken ct = default);
    Task<bool> DeleteEmployeeAsync(Guid id, Guid? changedBy, CancellationToken ct = default);
    Task AssignStructureRoleAsync(string kind, string nodeId, Guid employeeId, Guid? changedBy, string? reason, CancellationToken ct = default);
    Task ClearStructureRoleAsync(string kind, string nodeId, Guid? changedBy, string? reason, CancellationToken ct = default);
    Task<string> CreatePoleAsync(string name, CancellationToken ct = default);
    Task<string> CreateCelluleAsync(string poleId, string name, CancellationToken ct = default);
    Task<string> CreateServiceAsync(string celluleId, string name, CancellationToken ct = default);
    Task<bool> RenameOrgNodeAsync(OrgNodeLevel level, string nodeId, string name, CancellationToken ct = default);
    Task<bool> DeleteOrgNodeAsync(OrgNodeLevel level, string nodeId, CancellationToken ct = default);
    Task<bool> SetAuthSubjectIdAsync(Guid employeeId, Guid authSubjectId, CancellationToken ct = default);
}

public interface IDirectoryReconciliationService
{
    Task<DirectoryReconcileVerifyDto> VerifyAsync(CancellationToken ct = default);
    Task<DirectoryReconcileReportDto> ReconcileAsync(CancellationToken ct = default);
}

public interface IIamReadService
{
    Task<EffectivePermissionsDto> GetEffectivePermissionsAsync(Guid subjectId, string role, CancellationToken ct = default);
    Task<bool> EvaluateAsync(Guid subjectId, string role, string action, string resourceType, string? resourceId, CancellationToken ct = default);
}
