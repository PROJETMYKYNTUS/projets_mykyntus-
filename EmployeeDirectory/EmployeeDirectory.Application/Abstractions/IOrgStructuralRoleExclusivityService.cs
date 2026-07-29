using EmployeeDirectory.Application.Dtos;
using DomainAssignmentKind = EmployeeDirectory.Domain.Enums.OrgAssignmentKind;

namespace EmployeeDirectory.Application.Abstractions;

public interface IOrgStructuralRoleExclusivityService
{
    Task<IReadOnlyList<RevokedStructuralRoleDto>> RevokeAllStructuralRolesForEmployeeAsync(
        Guid employeeId,
        Guid? changedBy,
        string? reason,
        CancellationToken ct = default);

    /// <summary>
    /// Révoque les charges structurelles incompatibles avec <paramref name="keepKind"/>.
    /// Conserve les affectations actives du même kind (multi-pôles / multi-cellules / multi-services),
    /// sauf pour Pilote (une seule charge).
    /// </summary>
    Task<IReadOnlyList<RevokedStructuralRoleDto>> RevokeConflictingStructuralRolesForEmployeeAsync(
        Guid employeeId,
        DomainAssignmentKind keepKind,
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
