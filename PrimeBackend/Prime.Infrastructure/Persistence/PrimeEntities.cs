namespace Prime.Infrastructure.Persistence;

/// <summary>EF persistence for hiérarchie Pôle → Cellule → Service (Pilotes rattachés au Service).</summary>
public class PoleEntity
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public ICollection<CelluleEntity> Cellules { get; set; } = new List<CelluleEntity>();
}

public class CelluleEntity
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string PoleId { get; set; } = "";
    public PoleEntity Pole { get; set; } = null!;
    public ICollection<ServiceEntity> Services { get; set; } = new List<ServiceEntity>();
}

public class ServiceEntity
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string CelluleId { get; set; } = "";
    public CelluleEntity Cellule { get; set; } = null!;
    public ICollection<ServicePrimeIndicatorEntity> PrimeIndicators { get; set; } = new List<ServicePrimeIndicatorEntity>();
}

/// <summary>Indicateur PRIME propre à un service (libellé, pondérations, ordre).</summary>
public class ServicePrimeIndicatorEntity
{
    public Guid Id { get; set; }
    public string ServiceId { get; set; } = "";
    public ServiceEntity Service { get; set; } = null!;
    public int SortOrder { get; set; }
    public string Label { get; set; } = "";
    public decimal? PonderationPrimePct { get; set; }
    public decimal? PonderationChallengePct { get; set; }
    public bool IsActive { get; set; } = true;
    public string? TemplateStableId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>Archive autonome d'une fiche PRIME importée (employé introuvable ou nom libre).</summary>
public class PrimeHistoricalFicheEntity
{
    public Guid Id { get; set; }
    public string Period { get; set; } = "";
    public string CelluleId { get; set; } = "";
    public string? ServiceId { get; set; }
    public string RootPoleId { get; set; } = "";
    public string SupervisorUserId { get; set; } = "";
    /// <summary>Nom affiché lorsque l'employé n'existe pas en base.</summary>
    public string EmployeeExternalName { get; set; } = "";
    public string? EmployeeId { get; set; }
    public string? DetailGridJson { get; set; }
    public string? DetailGridPreviewSheetName { get; set; }
    public decimal? PrimeAmount { get; set; }
    public decimal? ChallengeAmount { get; set; }
    public decimal? TotalAmount { get; set; }
    public string? ServiceSaisieJson { get; set; }
    public string OriginFileName { get; set; } = "";
    /// <summary>Import | Manual</summary>
    public string Source { get; set; } = "Import";
    public string ImportedByUserId { get; set; } = "";
    public DateTimeOffset ImportedAt { get; set; }
}

public class EmployeeEntity
{
    public string Id { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Role { get; set; } = "";
    public string? ParentId { get; set; }
    public string? ServiceId { get; set; }
    public string? CelluleId { get; set; }
    public string PoleId { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Avatar { get; set; }
    public string? BusinessDepartmentId { get; set; }
    public string? BusinessDepartmentKind { get; set; }
}

public class BusinessDepartmentEntity
{
    public string Id { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "Operational";
    public string? ManagerEmployeeId { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<BusinessDepartmentPoleEntity> PoleAssignments { get; set; } = new List<BusinessDepartmentPoleEntity>();
}

public class BusinessDepartmentPoleEntity
{
    public Guid Id { get; set; }
    public string BusinessDepartmentId { get; set; } = "";
    public string PoleId { get; set; } = "";
    public BusinessDepartmentEntity BusinessDepartment { get; set; } = null!;
}

/// <summary>Instance de fiche PRIME saisie par un superviseur (période + template + JSON saisie / calcul).</summary>
public class SupervisorPrimeFicheEntity
{
    public Guid Id { get; set; }
    public string SupervisorUserId { get; set; } = "";
    public string? CelluleId { get; set; }
    /// <summary>Période cible, ex. 2026-04</summary>
    public string Period { get; set; } = "";
    public string TemplateId { get; set; } = "";
    public string TemplateDisplayName { get; set; } = "";
    public int TemplateFormatVersion { get; set; }
    /// <summary>Draft | Validated</summary>
    public string Status { get; set; } = "Draft";
    public string SchemaJson { get; set; } = "{}";
    public string SaisieJson { get; set; } = "{}";
    public string? ComputedJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ValidatedAt { get; set; }
}
