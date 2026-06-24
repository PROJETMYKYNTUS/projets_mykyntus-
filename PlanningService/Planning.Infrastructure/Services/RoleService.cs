using Microsoft.EntityFrameworkCore;
using Planning.Application.Abstractions;
using Planning.Application.DTOs;
using Planning.Infrastructure.Persistence;

namespace Planning.Infrastructure.Services;

public sealed class RoleService(AppDbContext context) : IRoleService
{
    public async Task<IReadOnlyList<RoleListItemDto>> GetActiveRolesAsync(CancellationToken ct = default) =>
        await context.Roles
            .Where(r => r.IsActive)
            .OrderBy(r => r.Name)
            .Select(r => new RoleListItemDto(r.Id, r.Name))
            .ToListAsync(ct);
}
