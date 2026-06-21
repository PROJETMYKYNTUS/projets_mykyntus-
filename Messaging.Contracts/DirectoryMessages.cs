namespace Kyntus.Messaging.Contracts;

/// <summary>Publié par Employee Directory (source canonique) après mutation employé.</summary>
public record DirectoryEmployeeChangedMessage
{
    public Guid EmployeeId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public Guid? ParentId { get; init; }
    public string? ServiceId { get; init; }
    public string? CelluleId { get; init; }
    public string? PoleId { get; init; }
    public Guid? BusinessDepartmentId { get; init; }
    public string? BusinessDepartmentKind { get; init; }
    public bool IsActive { get; init; } = true;
    public bool IsDeleted { get; init; }
}

/// <summary>Publié après mutation d'un département métier (Support / Operational).</summary>
public record DirectoryBusinessDepartmentChangedMessage
{
    public Guid BusinessDepartmentId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Kind { get; init; } = "Operational";
    public Guid? ManagerEmployeeId { get; init; }
    public IReadOnlyList<string> PoleIds { get; init; } = Array.Empty<string>();
    public bool IsDeleted { get; init; }
}

/// <summary>Publié après création/renommage d'un nœud org.</summary>
public record DirectoryOrgNodeChangedMessage
{
    public string NodeId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public OrgNodeLevel Level { get; init; }
    public string? ParentNodeId { get; init; }
    public bool IsDeleted { get; init; }
}

/// <summary>Publié après changement d'affectation structurelle.</summary>
public record DirectoryAssignmentChangedMessage
{
    public OrgAssignmentKind Kind { get; init; }
    public string NodeId { get; init; } = string.Empty;
    public OrgNodeLevel NodeLevel { get; init; }
    public Guid EmployeeId { get; init; }
    public string? EmployeeEmail { get; init; }
    public string? NewRole { get; init; }
    public bool Removed { get; init; }
}
