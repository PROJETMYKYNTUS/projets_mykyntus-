namespace Conge.Domain.Entities;

/// <summary>Miroir local d'un nœud org Directory (pôle / cellule / service).</summary>
public class OrgNodeConge
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    /// <summary>Pole | Cellule | Service</summary>
    public string Level { get; private set; } = "Service";
    public string? ParentId { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private OrgNodeConge() { }

    public static OrgNodeConge Creer(string id, string name, string level, string? parentId = null)
    {
        var nodeId = Normalize(id) ?? throw new ArgumentException("Id requis.", nameof(id));
        return new OrgNodeConge
        {
            Id = nodeId,
            Name = (name ?? string.Empty).Trim(),
            Level = NormalizeLevel(level),
            ParentId = Normalize(parentId),
            IsDeleted = false,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void MettreAJour(string name, string? parentId = null)
    {
        if (!string.IsNullOrWhiteSpace(name))
            Name = name.Trim();
        if (parentId is not null)
            ParentId = Normalize(parentId);
        IsDeleted = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarquerSupprime()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public static string NormalizeLevel(string? level)
    {
        var l = (level ?? "").Trim();
        if (l.Equals("Pole", StringComparison.OrdinalIgnoreCase)
            || l.Equals("0", StringComparison.Ordinal))
            return "Pole";
        if (l.Equals("Cellule", StringComparison.OrdinalIgnoreCase)
            || l.Equals("1", StringComparison.Ordinal))
            return "Cellule";
        return "Service";
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
