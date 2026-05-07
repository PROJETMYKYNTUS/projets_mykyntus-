namespace Conge.Domain.Entities;

/// <summary>
/// Snapshot des données employé reçues via RabbitMQ depuis le service RH/Planning.
/// On ne stocke que les infos nécessaires à la gestion des congés.
/// </summary>
public class EmployeSnapshot
{
    public Guid Id { get; private set; }
    public Guid EmployeId { get; private set; }
    public string Nom { get; private set; } = string.Empty;
    public string Prenom { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public Guid ManagerId { get; private set; }
    public Guid ServiceId { get; private set; }
    public string ServiceNom { get; private set; } = string.Empty;
    public DateTime DateEmbauche { get; private set; }
    public bool EstMineur { get; private set; }
    public DateTime DerniereModification { get; private set; }

    // EF Core constructor
    private EmployeSnapshot() { }

    public static EmployeSnapshot Creer(
        Guid employeId,
        string nom,
        string prenom,
        string email,
        Guid managerId,
        Guid serviceId,
        string serviceNom,
        DateTime dateEmbauche,
        bool estMineur = false)
    {
        return new EmployeSnapshot
        {
            Id = Guid.NewGuid(),
            EmployeId = employeId,
            Nom = nom,
            Prenom = prenom,
            Email = email,
            ManagerId = managerId,
            ServiceId = serviceId,
            ServiceNom = serviceNom,
            DateEmbauche = dateEmbauche,
            EstMineur = estMineur,
            DerniereModification = DateTime.UtcNow
        };
    }

    public void MettreAJour(
        string nom,
        string prenom,
        string email,
        Guid managerId,
        Guid serviceId,
        string serviceNom)
    {
        Nom = nom;
        Prenom = prenom;
        Email = email;
        ManagerId = managerId;
        ServiceId = serviceId;
        ServiceNom = serviceNom;
        DerniereModification = DateTime.UtcNow;
    }

    /// <summary>
    /// Calcule l'ancienneté en années complètes.
    /// </summary>
    public int GetAncienneteAnnees()
    {
        var today = DateTime.Today;
        var years = today.Year - DateEmbauche.Year;
        if (DateEmbauche.Date > today.AddYears(-years)) years--;
        return years;
    }

    /// <summary>
    /// Vérifie si l'employé a au moins 6 mois d'ancienneté (éligibilité congé annuel).
    /// </summary>
    public bool EstEligibleCongeAnnuel()
    {
        return DateTime.Today >= DateEmbauche.AddMonths(6);
    }
}