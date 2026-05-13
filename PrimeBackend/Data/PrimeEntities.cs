namespace PrimeBackend.Data;

/// <summary>EF persistence for hiérarchie Département → Pôle → Cellule → Équipe.</summary>
public class DepartmentEntity
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public ICollection<PoleEntity> Poles { get; set; } = new List<PoleEntity>();
}

public class PoleEntity
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string DepartmentId { get; set; } = "";
    public DepartmentEntity Department { get; set; } = null!;
    public ICollection<CelluleEntity> Cells { get; set; } = new List<CelluleEntity>();
}

public class CelluleEntity
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string PoleId { get; set; } = "";
    public PoleEntity Pole { get; set; } = null!;
    public ICollection<TeamEntity> Teams { get; set; } = new List<TeamEntity>();
    public ICollection<CellulePrimeIndicatorEntity> PrimeIndicators { get; set; } = new List<CellulePrimeIndicatorEntity>();
}

/// <summary>Indicateur PRIME propre à une cellule (libellé, pondérations, ordre).</summary>
public class CellulePrimeIndicatorEntity
{
    public Guid Id { get; set; }
    public string CelluleId { get; set; } = "";
    public CelluleEntity Cellule { get; set; } = null!;
    public int SortOrder { get; set; }
    public string Label { get; set; } = "";
    public decimal? PonderationPrimePct { get; set; }
    public decimal? PonderationChallengePct { get; set; }
    public bool IsActive { get; set; } = true;
    public string? TemplateStableId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>Saisie pôle RACC/SAV partagée (une fois par superviseur, pôle, période et template).</summary>
public class SupervisorPolePrimeDraftEntity
{
    public Guid Id { get; set; }
    public string SupervisorUserId { get; set; } = "";
    public string PoleId { get; set; } = "";
    public string Period { get; set; } = "";
    public string TemplateId { get; set; } = "";
    public string TemplateDisplayName { get; set; } = "";
    public int TemplateFormatVersion { get; set; }
    /// <summary>Draft | Validated</summary>
    public string Status { get; set; } = "Draft";
    public string SchemaJson { get; set; } = "{}";
    public string PoleSaisieJson { get; set; } = "{}";
    public string? ComputedJson { get; set; }
    /// <summary>Snapshot JSON (calcSheets, formulas, previewSheetName, etc.) pour recalcul HyperFormula côté client (pilotage).</summary>
    public string? TemplateCalcSnapshotJson { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<EmployeePrimeCellFicheEntity> EmployeeFiches { get; set; } = new List<EmployeePrimeCellFicheEntity>();
}

/// <summary>Partie « cellule » de la fiche PRIME pour un employé et une période.</summary>
public class EmployeePrimeCellFicheEntity
{
    public Guid Id { get; set; }
    public Guid PolePrimeDraftId { get; set; }
    public SupervisorPolePrimeDraftEntity PolePrimeDraft { get; set; } = null!;
    public string SupervisorUserId { get; set; } = "";
    public string EmployeeId { get; set; } = "";
    public string CelluleId { get; set; } = "";
    public string PoleId { get; set; } = "";
    public string Period { get; set; } = "";
    public string CellSaisieJson { get; set; } = "{}";
    /// <summary>NotStarted | InProgress | Complete</summary>
    public string FillingStatus { get; set; } = "NotStarted";
    public DateTimeOffset UpdatedAt { get; set; }
}

public class TeamEntity
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string CelluleId { get; set; } = "";
    public CelluleEntity Cellule { get; set; } = null!;
}

public class EmployeeEntity
{
    public string Id { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Role { get; set; } = "";
    public string? ParentId { get; set; }
    public string TeamId { get; set; } = "";
    public string DepartementId { get; set; } = "";
    public string PoleId { get; set; } = "";
    public string CelluleId { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Avatar { get; set; }
}

/// <summary>Instance de fiche PRIME saisie par un superviseur (période + template + JSON saisie / calcul).</summary>
public class SupervisorPrimeFicheEntity
{
    public Guid Id { get; set; }
    public string SupervisorUserId { get; set; } = "";
    public string? PoleId { get; set; }
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
