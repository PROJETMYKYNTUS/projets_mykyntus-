namespace Conge.Domain.Entities;

/// <summary>Portée d’un quota : cellule superviseur ou service référent.</summary>
public static class QuotaScopeKinds
{
    public const string Service = "Service";
    public const string Cellule = "Cellule";

    public static string Normalize(string? kind)
        => string.Equals(kind?.Trim(), Cellule, StringComparison.OrdinalIgnoreCase)
            ? Cellule
            : Service;
}

/// <summary>
/// Quota d'absences simultanées (employés absents le même jour) pour un nœud org
/// (cellule ou service). <see cref="ServiceId"/> = id Directory (ex. cell-… / svc-… ou Guid legacy).
/// </summary>
public class QuotaCongeService
{
    public Guid Id { get; private set; }
    /// <summary>Id du nœud org Directory (cellule ou service).</summary>
    public string ServiceId { get; private set; } = string.Empty;
    /// <summary><see cref="QuotaScopeKinds.Cellule"/> ou <see cref="QuotaScopeKinds.Service"/>.</summary>
    public string ScopeKind { get; private set; } = QuotaScopeKinds.Service;
    public int MaxAbsentsSimultanes { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    private QuotaCongeService() { }

    public static QuotaCongeService Creer(
        string serviceId,
        int maxAbsents,
        Guid? updatedBy = null,
        string? scopeKind = null)
    {
        var nodeId = NormalizeNodeId(serviceId)
            ?? throw new ArgumentException("ServiceId requis.", nameof(serviceId));
        if (maxAbsents < 1)
            throw new ArgumentException("Le quota doit être au moins 1.", nameof(maxAbsents));

        return new QuotaCongeService
        {
            Id = Guid.NewGuid(),
            ServiceId = nodeId,
            ScopeKind = QuotaScopeKinds.Normalize(scopeKind),
            MaxAbsentsSimultanes = maxAbsents,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = updatedBy
        };
    }

    public void MettreAJour(int maxAbsents, Guid? updatedBy = null, string? scopeKind = null)
    {
        if (maxAbsents < 1)
            throw new ArgumentException("Le quota doit être au moins 1.", nameof(maxAbsents));
        MaxAbsentsSimultanes = maxAbsents;
        if (!string.IsNullOrWhiteSpace(scopeKind))
            ScopeKind = QuotaScopeKinds.Normalize(scopeKind);
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public static string? NormalizeNodeId(string? raw)
    {
        var id = raw?.Trim();
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }
}
