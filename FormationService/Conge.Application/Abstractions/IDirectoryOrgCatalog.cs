namespace Conge.Application.Abstractions;

/// <summary>Catalogue org Directory (noms + effectifs) pour Congés.</summary>
public interface IDirectoryOrgCatalog
{
    Task<DirectoryOrgCatalogSnapshot> GetSnapshotAsync(CancellationToken ct = default);
    Task<string?> ResolveNodeNameAsync(string nodeId, CancellationToken ct = default);
}

public sealed record DirectoryOrgCatalogSnapshot(
    IReadOnlyDictionary<string, string> NodeNames,
    IReadOnlyDictionary<string, int> CelluleHeadcounts,
    IReadOnlyDictionary<string, int> ServiceHeadcounts)
{
    public static DirectoryOrgCatalogSnapshot Empty { get; } = new(
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));

    public string? GetName(string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return null;
        return NodeNames.TryGetValue(nodeId.Trim(), out var n) && !string.IsNullOrWhiteSpace(n) ? n : null;
    }

    public int GetHeadcount(string nodeId, string scopeKind)
    {
        var id = nodeId?.Trim() ?? "";
        if (id.Length == 0) return 0;
        if (string.Equals(scopeKind, "Cellule", StringComparison.OrdinalIgnoreCase))
            return CelluleHeadcounts.TryGetValue(id, out var c) ? c : 0;
        return ServiceHeadcounts.TryGetValue(id, out var s) ? s : 0;
    }
}
