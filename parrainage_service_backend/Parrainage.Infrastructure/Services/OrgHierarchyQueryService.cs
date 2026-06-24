using Microsoft.EntityFrameworkCore;
using Parrainage.Application.Abstractions;
using Parrainage.Infrastructure.Persistence;

namespace Parrainage.Infrastructure.Services;

public sealed class OrgHierarchyQueryService(ParrainageDbContext db) : IOrgHierarchyQueryService
{
    public async Task<IReadOnlyList<OrgNodeDto>> ListNodesAsync(CancellationToken ct = default) =>
        await db.PortalUsers.AsNoTracking()
            .OrderBy(u => u.Email)
            .Select(u => new OrgNodeDto(u.Id, u.ParentId, u.Email, u.Role, u.Name))
            .ToListAsync(ct);
}
