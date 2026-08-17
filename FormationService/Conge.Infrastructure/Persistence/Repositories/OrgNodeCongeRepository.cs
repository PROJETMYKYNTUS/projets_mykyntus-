using Conge.Domain.Entities;
using Conge.Domain.Interfaces;
using Conge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Conge.Infrastructure.Persistence.Repositories;

public sealed class OrgNodeCongeRepository(CongeDbContext context) : IOrgNodeCongeRepository
{
    public async Task<OrgNodeConge?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var key = QuotaCongeService.NormalizeNodeId(id);
        if (key is null) return null;
        return await context.OrgNodesConge.FirstOrDefaultAsync(n => n.Id == key, ct);
    }

    public async Task<IReadOnlyList<OrgNodeConge>> GetAllActiveAsync(CancellationToken ct = default)
        => await context.OrgNodesConge.AsNoTracking()
            .Where(n => !n.IsDeleted)
            .ToListAsync(ct);

    public async Task UpsertAsync(string id, string name, string level, string? parentId, CancellationToken ct = default)
    {
        var key = QuotaCongeService.NormalizeNodeId(id)
            ?? throw new ArgumentException("Id requis.", nameof(id));
        var existing = await context.OrgNodesConge.FirstOrDefaultAsync(n => n.Id == key, ct);
        if (existing is null)
        {
            await context.OrgNodesConge.AddAsync(OrgNodeConge.Creer(key, name, level, parentId), ct);
        }
        else
        {
            existing.MettreAJour(name, parentId);
            // Level is immutable after create for simplicity; recreate if wrong is rare.
        }
    }

    public async Task MarkDeletedAsync(string id, CancellationToken ct = default)
    {
        var key = QuotaCongeService.NormalizeNodeId(id);
        if (key is null) return;
        var existing = await context.OrgNodesConge.FirstOrDefaultAsync(n => n.Id == key, ct);
        existing?.MarquerSupprime();
    }
}
