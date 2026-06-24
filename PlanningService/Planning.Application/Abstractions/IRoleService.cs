using Planning.Application.DTOs;

namespace Planning.Application.Abstractions;

public interface IRoleService
{
    Task<IReadOnlyList<RoleListItemDto>> GetActiveRolesAsync(CancellationToken ct = default);
}
