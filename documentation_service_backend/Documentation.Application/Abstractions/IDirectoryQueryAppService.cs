using Documentation.Application.Api;

namespace Documentation.Application.Abstractions;

public interface IDirectoryQueryAppService
{
    Task<IReadOnlyList<DirectoryUserResponse>> ListUsersAsync(CancellationToken ct = default);
    Task<DirectoryUserResponse?> GetUserAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationalUnitSummary>> GetPolesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationalUnitSummary>> GetCellulesByPoleAsync(Guid poleId, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationalUnitSummary>> GetDepartementsByCelluleAsync(Guid celluleId, CancellationToken ct = default);
    Task<IReadOnlyList<DirectoryUserResponse>> GetUsersByRoleAndOrgAsync(
        string role,
        Guid poleId,
        Guid celluleId,
        Guid departementId,
        CancellationToken ct = default);
    Task<IReadOnlyList<DirectoryUserResponse>> GetManagersByDepartementAsync(Guid departementId, CancellationToken ct = default);
    Task<IReadOnlyList<DirectoryUserResponse>> GetCoachesByManagerAsync(Guid managerId, Guid? departementId, CancellationToken ct = default);
    Task<IReadOnlyList<DirectoryUserResponse>> GetPilotesByCoachAsync(Guid coachId, Guid? departementId, CancellationToken ct = default);
}
