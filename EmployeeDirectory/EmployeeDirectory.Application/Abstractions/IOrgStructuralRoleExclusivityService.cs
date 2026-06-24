using EmployeeDirectory.Application.Dtos;

namespace EmployeeDirectory.Application.Abstractions;

public interface IOrgStructuralRoleExclusivityService
{
    Task<IReadOnlyList<RevokedStructuralRoleDto>> RevokeAllStructuralRolesForEmployeeAsync(
        Guid employeeId,
        Guid? changedBy,
        string? reason,
        CancellationToken ct = default);

    Task DemotePreviousDepartmentManagerAsync(
        Guid departmentId,
        Guid newManagerId,
        Guid? changedBy,
        string? reason,
        CancellationToken ct = default);
}
