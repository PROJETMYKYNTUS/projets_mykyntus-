namespace Planning.Messaging.Messages;
// ── Messages consommés depuis le service RH ───────────────────────────────────

/// <summary>
/// Reçu depuis RH quand un employé est créé → initialiser son solde.
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
}

/// <summary>
/// Reçu depuis RH quand les infos d'un employé changent (manager, service...).
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
}

/// <summary>
/// Reçu depuis RH en début d'année → initialise le solde annuel.
/// </summary>
public record SoldeAnnuelInitialiseMessage
{
    public Guid EmployeId { get; init; }
    public int AncienneteAnnees { get; init; }
    public bool EstMineur { get; init; }
    public int Annee { get; init; }
}

// ── Messages publiés vers les autres services ─────────────────────────────────

/// <summary>
/// Publié vers RH/Planning quand un congé est validé.
/// </summary>
public record CongeValideMessage
{
    public Guid DemandeId { get; init; }
    public Guid EmployeId { get; init; }
    public DateTime DateDebut { get; init; }
    public DateTime DateFin { get; init; }
    public double NombreJours { get; init; }
    public DateTime DateValidation { get; init; }
}

/// <summary>
/// Publié quand une demande est soumise (pour notifier le manager).
/// </summary>
public record CongeDemandeMessage
{
    public Guid DemandeId { get; init; }
    public Guid EmployeId { get; init; }
    public Guid ManagerId { get; init; }
    public DateTime DateDebut { get; init; }
    public DateTime DateFin { get; init; }
}

/// <summary>
/// Publié quand un congé est refusé.
/// </summary>
public record CongeRefuseMessage
{
    public Guid DemandeId { get; init; }
    public Guid EmployeId { get; init; }
    public string Motif { get; init; } = string.Empty;
}