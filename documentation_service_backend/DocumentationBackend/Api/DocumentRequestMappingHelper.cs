using DocumentationBackend.Data;
using DocumentationBackend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocumentationBackend.Api;

internal static class DocumentRequestMappingHelper
{
    /// <summary>Dernier PDF généré par demande (plus récent <c>created_at</c>).</summary>
    internal static async Task<IReadOnlyDictionary<Guid, GeneratedDocument>> LoadLatestGeneratedByRequestIdsAsync(
        DocumentationDbContext db,
        IEnumerable<Guid> requestIds,
        CancellationToken ct)
    {
        var ids = requestIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, GeneratedDocument>();

        var rows = await db.GeneratedDocuments.AsNoTracking()
            .Where(g => g.DocumentRequestId != null && ids.Contains(g.DocumentRequestId.Value))
            .ToListAsync(ct);

        return rows
            .GroupBy(g => g.DocumentRequestId!.Value)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var official = g.Where(x => x.Status == GeneratedDocumentStatus.Generated).MaxBy(x => x.CreatedAt);
                    return official ?? g.MaxBy(x => x.CreatedAt)!;
                });
    }

    internal static async Task<GeneratedDocument?> LoadLatestGeneratedForRequestAsync(
        DocumentationDbContext db,
        Guid requestId,
        CancellationToken ct)
    {
        return await db.GeneratedDocuments.AsNoTracking()
            .Where(g => g.DocumentRequestId == requestId)
            .OrderByDescending(g => g.Status == GeneratedDocumentStatus.Generated)
            .ThenByDescending(g => g.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    internal static async Task<IReadOnlyDictionary<Guid, string>> LoadDisplayNamesAsync(
        DocumentationDbContext db,
        IEnumerable<Guid> ids,
        CancellationToken ct)
    {
        var set = ids.Where(g => g != Guid.Empty).Distinct().ToList();
        if (set.Count == 0)
            return new Dictionary<Guid, string>();

        var rows = await db.DirectoryUsers.AsNoTracking()
            .Where(u => set.Contains(u.Id))
            .Select(u => new { u.Id, u.Prenom, u.Nom })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Id, r => $"{r.Prenom} {r.Nom}".Trim());
    }

    internal static string ResolveName(IReadOnlyDictionary<Guid, string> names, Guid id) =>
        names.TryGetValue(id, out var n) ? n : $"Utilisateur {id}";
}
