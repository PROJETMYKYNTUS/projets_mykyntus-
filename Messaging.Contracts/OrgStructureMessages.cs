namespace Kyntus.Messaging.Contracts;

public enum OrgNodeLevel
{
    Pole = 0,
    Cellule = 1,
    Service = 2
}

public enum OrgAssignmentKind
{
    ChefDeProjet = 0,
    Superviseur = 1,
    ReferentTechnique = 2,
    Pilote = 3
}

/// <summary>
/// Publié quand un nœud org Prime est créé.
/// </summary>
public record OrgNodeCreatedMessage
{
    public string NodeId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public OrgNodeLevel Level { get; init; }
    public string? ParentNodeId { get; init; }
    public string Code { get; init; } = string.Empty;
}

/// <summary>
/// Publié quand un nœud org Prime est renommé.
/// </summary>
public record OrgNodeRenamedMessage
{
    public string NodeId { get; init; } = string.Empty;
    public OrgNodeLevel Level { get; init; }
    public string NewName { get; init; } = string.Empty;
}

/// <summary>
/// Publié quand une affectation responsable/pilote change sur l'org Prime.
/// </summary>
public record OrgAssignmentChangedMessage
{
    public OrgAssignmentKind Kind { get; init; }
    public string NodeId { get; init; } = string.Empty;
    public OrgNodeLevel NodeLevel { get; init; }
    public string EmployeeId { get; init; } = string.Empty;
    public string? EmployeeEmail { get; init; }
    public string? ParentEmployeeId { get; init; }
    public bool Removed { get; init; }
}
