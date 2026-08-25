namespace Prime.Domain.Entities;

/// <summary>Saisie cellule RACC/SAV partagée (une fois par superviseur, cellule, période et template).</summary>
public class SupervisorCellulePrimeDraft
{
    public Guid Id { get; set; }
    public string SupervisorUserId { get; set; } = "";
    public string RootPoleId { get; set; } = "";
    public string CelluleId { get; set; } = "";
    public string Period { get; set; } = "";
    public string TemplateId { get; set; } = "";
    public string TemplateDisplayName { get; set; } = "";
    public int TemplateFormatVersion { get; set; }
    public string Status { get; set; } = "Draft";
    public string SchemaJson { get; set; } = "{}";
    public string CelluleSaisieJson { get; set; } = "{}";
    public string? ComputedJson { get; set; }
    public string? TemplateCalcSnapshotJson { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
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
    public ICollection<EmployeePrimeServiceFiche> EmployeeFiches { get; set; } = new List<EmployeePrimeServiceFiche>();
}

/// <summary>Partie « service » de la fiche PRIME pour un employé et une période.</summary>
public class EmployeePrimeServiceFiche
{
    public Guid Id { get; set; }
    public Guid CellulePrimeDraftId { get; set; }
    public SupervisorCellulePrimeDraft CellulePrimeDraft { get; set; } = null!;
    public string SupervisorUserId { get; set; } = "";
    public string EmployeeId { get; set; } = "";
    public string ServiceId { get; set; } = "";
    public string CelluleId { get; set; } = "";
    public string Period { get; set; } = "";
    public string ServiceSaisieJson { get; set; } = "{}";
    public string FillingStatus { get; set; } = "NotStarted";
    public DateTimeOffset UpdatedAt { get; set; }
    public string ValidationStatus { get; set; } = "AwaitingData";
    public string? LastApproverUserId { get; set; }
    public DateTimeOffset? LastApprovedAt { get; set; }
    public string? RejectedByUserId { get; set; }
    public DateTimeOffset? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public decimal? PrimeAmount { get; set; }
    public decimal? ChallengeAmount { get; set; }
    public decimal? TotalAmount { get; set; }
    public string? DetailGridJson { get; set; }
    public string? DetailGridPreviewSheetName { get; set; }
    public string? TemplateVersionRef { get; set; }
    public DateTimeOffset? DetailGridFrozenAt { get; set; }
    /// <summary>Pondérations partie commune réellement utilisées, figées à la finalisation.</summary>
    public string? PonderationsSnapshotJson { get; set; }
    public ICollection<EmployeePrimeFicheValidationHistory> ValidationHistory { get; set; } =
        new List<EmployeePrimeFicheValidationHistory>();
}

/// <summary>Historique immuable des transitions de validation d'une fiche pilote.</summary>
public class EmployeePrimeFicheValidationHistory
{
    public Guid Id { get; set; }
    public Guid FicheId { get; set; }
    public EmployeePrimeServiceFiche Fiche { get; set; } = null!;
    public DateTimeOffset At { get; set; }
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
