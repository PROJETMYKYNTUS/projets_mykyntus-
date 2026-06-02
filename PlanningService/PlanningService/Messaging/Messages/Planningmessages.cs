namespace Planning.Messaging.Messages;

/// <summary>
/// Publié quand un nouvel employé est créé dans Planning.
/// → Conge Service écoute cet event pour créer le EmployeSnapshot
///   et initialiser le solde de congé.
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
/// Reçu depuis Auth Service après création du compte auth.
/// → Planning Service met à jour AuthUserId sur l'employé.
/// </summary>
public record AuthUserCreatedMessage
{
    public string Email { get; init; } = string.Empty;
    public int AuthUserId { get; init; }
}
/// <summary>
/// Publié quand les infos d'un employé changent (manager, service, email...).
/// → Conge Service met à jour le EmployeSnapshot local.
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
/// Publié en début de chaque année (ex: job planifié le 1er janvier).
/// → Conge Service crée un nouveau SoldeConge pour l'année.
/// </summary>
public record SoldeAnnuelInitialiseMessage
{
    public Guid EmployeId { get; init; }
    public int AncienneteAnnees { get; init; }
    public bool EstMineur { get; init; }
    public int Annee { get; init; }
}

/// <summary>
/// Reçu depuis Conge Service quand un congé est validé.
/// → Planning peut mettre à jour le calendrier / absences.
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