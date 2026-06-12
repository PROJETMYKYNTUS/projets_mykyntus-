namespace Kyntus.Messaging.Contracts;

/// <summary>
/// Publié quand un nouvel employé est créé dans Planning (maître RH).
/// </summary>
public record EmployeCreatedMessage
{
    public Guid EmployeId { get; init; }
    public string Nom { get; init; } = string.Empty;
    public string Prenom { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public Guid ManagerId { get; init; }
    public Guid ServiceId { get; init; }
    public string ServiceNom { get; init; } = string.Empty;
    public DateTime DateEmbauche { get; init; }
    public bool EstMineur { get; init; }
    public string Role { get; init; } = string.Empty;
    public int? SubServiceId { get; init; }
    public string? PrimeServiceId { get; init; }
    public Guid SupervisorId { get; init; }
}

/// <summary>
/// Publié quand les infos d'un employé changent (manager, service, rôle…).
/// </summary>
public record EmployeUpdatedMessage
{
    public Guid EmployeId { get; init; }
    public string Nom { get; init; } = string.Empty;
    public string Prenom { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public Guid ManagerId { get; init; }
    public Guid ServiceId { get; init; }
    public string ServiceNom { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public int? SubServiceId { get; init; }
    public string? PrimeServiceId { get; init; }
    public Guid SupervisorId { get; init; }
}

/// <summary>
/// Publié en début d'année pour initialiser le solde de congé.
/// </summary>
public record SoldeAnnuelInitialiseMessage
{
    public Guid EmployeId { get; init; }
    public int AncienneteAnnees { get; init; }
    public bool EstMineur { get; init; }
    public int Annee { get; init; }
}
