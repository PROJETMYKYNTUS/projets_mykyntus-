public class UserDto
{
    public int Id { get; set; }
    public Guid Guid { get; set; }
    public int? AuthUserId { get; set; }
    public Guid? ManagerId { get; set; }
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public int? SubServiceId { get; set; }
    public string? SubServiceName { get; set; }
    public string? OrgPoleName { get; set; }
    public string? OrgCelluleName { get; set; }
    public string? OrgServiceName { get; set; }
    public string? OrgOperationalDepartmentName { get; set; }
    public List<SubServiceSimpleDto> ManagedSubServices { get; set; } = new();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime HireDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Level { get; set; }
    public int? NiveauExpertiseMetier { get; set; }
    public Guid? ChefDeProjetId { get; set; }
    public Guid? SuperviseurId { get; set; }
    public Guid? ReferentTechniqueId { get; set; }
    public UserHrProfileDto? HrProfile { get; set; }
    public List<ServiceSimpleDto> ManagedServices { get; set; } = new();
    public Dictionary<string, string?> CustomFields { get; set; } = new();
}

public class UserHrProfileDto
{
    public DateOnly? DateNaissance { get; set; }
    public string? VilleNaissance { get; set; }
    public string? Nationalite { get; set; }
    public string? NumeroCarteAutoentrepreneur { get; set; }
    public string? Sexe { get; set; }
    public string? SituationFamiliale { get; set; }
    public int? NombreEnfants { get; set; }
    public string? Cin { get; set; }
    public string? Adresse { get; set; }
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
}

public class UpdateContractualLevelDto
{
    public int Level { get; set; }
}

public class SubServiceSimpleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
}

public class CreateUserDto
{
    public int RoleId { get; set; }
    public int? SubServiceId { get; set; }
    public List<int> ManagedSubServiceIds { get; set; } = new();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime HireDate { get; set; }
    public string Email { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public int? NiveauExpertiseMetier { get; set; }
    public Guid? ChefDeProjetId { get; set; }
    public Guid? SuperviseurId { get; set; }
    public Guid? ReferentTechniqueId { get; set; }
    public UserHrProfileDto? HrProfile { get; set; }
    public List<int> ManagedServiceIds { get; set; } = new();
    public Dictionary<string, string?> CustomFields { get; set; } = new();
}

public class UpdateUserDto
{
    public int RoleId { get; set; }
    public int? SubServiceId { get; set; }
    public List<int> ManagedSubServiceIds { get; set; } = new();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime HireDate { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int Level { get; set; } = 1;
    public int? NiveauExpertiseMetier { get; set; }
    public Guid? ChefDeProjetId { get; set; }
    public Guid? SuperviseurId { get; set; }
    public Guid? ReferentTechniqueId { get; set; }
    public UserHrProfileDto? HrProfile { get; set; }
    public List<int> ManagedServiceIds { get; set; } = new();
    public Dictionary<string, string?> CustomFields { get; set; } = new();
}
public class ServiceSimpleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FloorName { get; set; } = string.Empty;
}
