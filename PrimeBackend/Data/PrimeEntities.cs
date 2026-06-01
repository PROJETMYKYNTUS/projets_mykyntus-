namespace PrimeBackend.Data;

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

/// <summary>Saisie cellule RACC/SAV partagée (une fois par superviseur, cellule, période et template).</summary>
public class SupervisorCellulePrimeDraftEntity
{
    public Guid Id { get; set; }
    public string SupervisorUserId { get; set; } = "";
    /// <summary>Pôle racine EF (<c>prime_pole</c>) — unicité fiche commune par période.</summary>
    public string RootPoleId { get; set; } = "";
    public string CelluleId { get; set; } = "";
    public string Period { get; set; } = "";
    public string TemplateId { get; set; } = "";
    public string TemplateDisplayName { get; set; } = "";
    public int TemplateFormatVersion { get; set; }
    /// <summary>Draft | Validated</summary>
    public string Status { get; set; } = "Draft";
    public string SchemaJson { get; set; } = "{}";
    public string CelluleSaisieJson { get; set; } = "{}";
    public string? ComputedJson { get; set; }
    /// <summary>Snapshot JSON (calcSheets, formulas, previewSheetName, etc.) pour recalcul HyperFormula côté client (pilotage).</summary>
    public string? TemplateCalcSnapshotJson { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Fichier Excel « pool global » (montants / données administratives) — validations RH + Manager puis accusé Compta.</summary>
    public byte[]? GlobalPoolExcelContent { get; set; }
    public string? GlobalPoolFileName { get; set; }
    public DateTimeOffset? GlobalPoolUploadedAt { get; set; }
    public string? GlobalPoolUploadedByUserId { get; set; }
    public DateTimeOffset? GlobalPoolManagerApprovedAt { get; set; }
    public string? GlobalPoolManagerApprovedByUserId { get; set; }
    public DateTimeOffset? GlobalPoolRhApprovedAt { get; set; }
    public string? GlobalPoolRhApprovedByUserId { get; set; }
    public DateTimeOffset? GlobalPoolComptaAckAt { get; set; }
    public string? GlobalPoolComptaAckByUserId { get; set; }

    public ICollection<EmployeePrimeServiceFicheEntity> EmployeeFiches { get; set; } = new List<EmployeePrimeServiceFicheEntity>();
    public ICollection<GlobalPoolApprovalEntity> GlobalPoolApprovals { get; set; } = new List<GlobalPoolApprovalEntity>();
}

/// <summary>Partie « service » de la fiche PRIME pour un employé et une période.</summary>
public class EmployeePrimeServiceFicheEntity
{
    public Guid Id { get; set; }
    public Guid CellulePrimeDraftId { get; set; }
    public SupervisorCellulePrimeDraftEntity CellulePrimeDraft { get; set; } = null!;
    public string SupervisorUserId { get; set; } = "";
    public string EmployeeId { get; set; } = "";
    public string ServiceId { get; set; } = "";
    public string CelluleId { get; set; } = "";
    public string Period { get; set; } = "";
    public string ServiceSaisieJson { get; set; } = "{}";
    /// <summary>NotStarted | InProgress | Complete</summary>
    public string FillingStatus { get; set; } = "NotStarted";
    public DateTimeOffset UpdatedAt { get; set; }

    // ============================================================
    // Workflow de validation — 4 étapes actives (+ Rejected)
    // Pending → Superviseur Approved → Chef de projet Approved → RH Approved
    // (Référent technique = lecture seule, pas de transition depuis Pending)
    // ============================================================
    /// <summary>AwaitingData | Pending | Superviseur Approved | Chef de projet Approved | RH Approved | Rejected</summary>
    public string ValidationStatus { get; set; } = "AwaitingData";
    /// <summary>UserId du dernier valideur (Superviseur, Chef de projet ou RH).</summary>
    public string? LastApproverUserId { get; set; }
    public DateTimeOffset? LastApprovedAt { get; set; }
    /// <summary>UserId du rejeteur (si Rejected).</summary>
    public string? RejectedByUserId { get; set; }
    public DateTimeOffset? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    /// <summary>Montants snapshot (export) ; affichage validation = extraction live depuis ServiceSaisieJson.</summary>
    public decimal? PrimeAmount { get; set; }
    public decimal? ChallengeAmount { get; set; }
    public decimal? TotalAmount { get; set; }

    public ICollection<EmployeePrimeFicheValidationHistoryEntity> ValidationHistory { get; set; } =
        new List<EmployeePrimeFicheValidationHistoryEntity>();
}

/// <summary>Historique immuable des transitions de validation d'une fiche pilote.</summary>
public class EmployeePrimeFicheValidationHistoryEntity
{
    public Guid Id { get; set; }
    public Guid FicheId { get; set; }
    public EmployeePrimeServiceFicheEntity Fiche { get; set; } = null!;
    public DateTimeOffset At { get; set; }
    /// <summary>Approved | Rejected</summary>
    public string Action { get; set; } = "";
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";
    public string ActorUserId { get; set; } = "";
    public string ActorRole { get; set; } = "";
    public string? ActorDisplayName { get; set; }
    public string? Comment { get; set; }
    public decimal? PrimeAmount { get; set; }
    public decimal? ChallengeAmount { get; set; }
    public decimal? TotalAmount { get; set; }
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
