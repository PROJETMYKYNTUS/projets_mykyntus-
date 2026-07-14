namespace EmployeeDirectory.Domain.Entities;

/// <summary>Profil RH canonique 1-1 avec <see cref="Employee"/>.</summary>
public class EmployeeHrProfile
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public DateOnly? DateNaissance { get; set; }
    public string? VilleNaissance { get; set; }
    public string? Nationalite { get; set; }
    public string? NumeroCarteAutoentrepreneur { get; set; }
    public string? Sexe { get; set; }
    public string? SituationFamiliale { get; set; }
    public int? NombreEnfants { get; set; }
    public string? Cin { get; set; }
    public string? Adresse { get; set; }
    /// <summary>Email personnel (hors compte / login).</summary>
    public string? EmailPersonnel { get; set; }
    public string? Telephone1 { get; set; }
    public string? TelephoneUrgence { get; set; }
    public string? RelationUrgence { get; set; }

    public string? Rib { get; set; }
    public string? ImmatriculationInterne { get; set; }
    public string? ImmatriculationCnss { get; set; }

    public DateOnly? DateEntree { get; set; }
    public DateOnly? DateEmbauche { get; set; }
    public DateOnly? DateAnciennete { get; set; }
    public DateOnly? DateSortie { get; set; }
    public DateOnly? DateEvolutionPoste { get; set; }
    public string? AncienPoste { get; set; }
    public string? AncienService { get; set; }
    public string? NiveauScolaire { get; set; }
    public string? IntitulesEtudes { get; set; }

    public bool EnFormation { get; set; }
    public DateOnly? DateDebutFormation { get; set; }
    public DateOnly? DateFinFormationPrevue { get; set; }
    /// <summary>Niveau d'expertise métier (1=Débutant, 2=Confirmé, 3=Expert).</summary>
    public int? NiveauExpertiseMetier { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
