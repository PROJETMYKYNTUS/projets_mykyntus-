namespace Prime.Application.DTOs;

public sealed class ServicePrimeIndicatorDto
{
    public Guid Id { get; init; }
    public string ServiceId { get; init; } = "";
    public int SortOrder { get; init; }
    public string Label { get; init; } = "";
    public decimal? PonderationPrimePct { get; init; }
    public decimal? PonderationChallengePct { get; init; }
    public bool IsActive { get; init; }
    public string? TemplateStableId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

public sealed class PutServicePrimeIndicatorsRequest
{
    public List<PutServicePrimeIndicatorItem> Indicators { get; set; } = [];
}

public sealed class PutServicePrimeIndicatorItem
{
    public int SortOrder { get; set; }
    public string Label { get; set; } = "";
    public decimal? PonderationPrimePct { get; set; }
    public decimal? PonderationChallengePct { get; set; }
    public bool IsActive { get; set; } = true;
    public string? TemplateStableId { get; set; }
}

public sealed class ServicePoleLinePonderationDto
{
    public Guid Id { get; init; }
    public string ServiceId { get; init; } = "";
    public string TemplateStableId { get; init; } = "";
    public string Label { get; init; } = "";
    public int SortOrder { get; init; }
    public decimal? PonderationPrimePct { get; init; }
    public decimal? PonderationChallengePct { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

public sealed class PutServicePoleLinePonderationsRequest
{
    public List<PutServicePoleLinePonderationItem> Items { get; set; } = [];
}

public sealed class PutServicePoleLinePonderationItem
{
    public string TemplateStableId { get; set; } = "";
    public string Label { get; set; } = "";
    public int SortOrder { get; set; }
    public decimal? PonderationPrimePct { get; set; }
    public decimal? PonderationChallengePct { get; set; }
}

public sealed class SupervisorCellulePrimeDraftResponseDto
{
    public Guid Id { get; init; }
    public string SupervisorUserId { get; init; } = "";
    public string CelluleId { get; init; } = "";
    public string Period { get; init; } = "";
    public string TemplateId { get; init; } = "";
    public string TemplateDisplayName { get; init; } = "";
    public int TemplateFormatVersion { get; init; }
    public string Status { get; init; } = "";
    public string SchemaJson { get; init; } = "{}";
    public string CelluleSaisieJson { get; init; } = "{}";
    public string? ComputedJson { get; init; }
    public string? TemplateCalcSnapshotJson { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class UpsertSupervisorCellulePrimeDraftRequest
{
    public string SupervisorUserId { get; set; } = "";
    public string CelluleId { get; set; } = "";
    /// <summary>Clé JSON héritée (module Angular / ancien contrat « pôle ») — mappée sur <see cref="CelluleId"/> si celle-ci est vide.</summary>
    public string? PoleId { get; set; }
    public string Period { get; set; } = "";
    public string TemplateId { get; set; } = "";
    public string TemplateDisplayName { get; set; } = "";
    public int TemplateFormatVersion { get; set; }
    public string SchemaJson { get; set; } = "{}";
    public string CelluleSaisieJson { get; set; } = "{}";
    /// <summary>Clé JSON héritée — utilisée si <see cref="CelluleSaisieJson"/> est absent ou « {{}} ».</summary>
    public string? PoleSaisieJson { get; set; }
    public string? ComputedJson { get; set; }
    public string? TemplateCalcSnapshotJson { get; set; }
    public string? Status { get; set; }
}

/// <summary>Item de la liste des fiches communes « en cours » d'un superviseur — agrège la progression service pour filtrer celles totalement terminées (Validated + tous employés Complete).</summary>
public sealed class SupervisorCellulePrimeDraftListItemDto
{
    public Guid Id { get; init; }
    public string SupervisorUserId { get; init; } = "";
    public string CelluleId { get; init; } = "";
    public string Period { get; init; } = "";
    public string TemplateId { get; init; } = "";
    public string TemplateDisplayName { get; init; } = "";
    public int TemplateFormatVersion { get; init; }
    /// <summary>Draft | Validated</summary>
    public string Status { get; init; } = "Draft";
    public int TotalEmployees { get; init; }
    public int CompleteEmployees { get; init; }
    public int InProgressEmployees { get; init; }
    public int NotStartedEmployees { get; init; }
    /// <summary>True si Status=Validated et tous les employés sont en Complete (la ligne est filtrée côté liste).</summary>
    public bool IsFullyComplete { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public bool HasGlobalPoolFile { get; init; }
    /// <summary>True lorsque Manager et RH ont tous deux validé le fichier global (déblocage diffusion pilotes / compta).</summary>
    public bool PoolDistributionUnlocked { get; init; }
}

public sealed class EmployeePrimeServiceFicheResponseDto
{
    public Guid Id { get; init; }
    public Guid CellulePrimeDraftId { get; init; }
    public string SupervisorUserId { get; init; } = "";
    public string EmployeeId { get; init; } = "";
    public string ServiceId { get; init; } = "";
    public string CelluleId { get; init; } = "";
    public string Period { get; init; } = "";
    public string ServiceSaisieJson { get; init; } = "{}";
    public string FillingStatus { get; init; } = "";
    public string ValidationStatus { get; init; } = "";
    public bool IsReadyForValidation { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class UpsertEmployeePrimeServiceFicheRequest
{
    public string SupervisorUserId { get; set; } = "";
    public string EmployeeId { get; set; } = "";
    public string Period { get; set; } = "";
    public Guid CellulePrimeDraftId { get; set; }
    /// <summary>Clé JSON héritée (module Angular) — mappée sur <see cref="CellulePrimeDraftId"/> si vide.</summary>
    public Guid? PolePrimeDraftId { get; set; }
    public string ServiceSaisieJson { get; set; } = "{}";
    /// <summary>Clé JSON héritée — mappée sur <see cref="ServiceSaisieJson"/> si celle-ci est vide ou « {{}} ».</summary>
    public string? CellSaisieJson { get; set; }
}

/// <summary>Montants finaux (ligne « TOTAL Général » de la fiche fusionnée) persistés sur la fiche.</summary>
public sealed class PersistFicheAmountsRequest
{
    public string SupervisorUserId { get; set; } = "";
    public decimal? PrimeAmount { get; set; }
    public decimal? ChallengeAmount { get; set; }
    public decimal? TotalAmount { get; set; }
}

public sealed class EmployeePrimeServiceFicheListItemDto
{
    public string EmployeeId { get; init; } = "";
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string Email { get; init; } = "";
    public string ServiceId { get; init; } = "";
    public Guid? FicheId { get; init; }
    public Guid? CellulePrimeDraftId { get; init; }
    public string FillingStatus { get; init; } = "NotStarted";
    public string? ValidationStatus { get; init; }
    public bool? IsReadyForValidation { get; init; }
    public string ServiceSaisieJson { get; init; } = "{}";
    public DateTimeOffset? UpdatedAt { get; init; }
}

public sealed class ServicePilotageSummaryDto
{
    public string ServiceId { get; init; } = "";
    public string ServiceName { get; init; } = "";
    public string CelluleId { get; init; } = "";
    public string CelluleName { get; init; } = "";
    public string PoleName { get; init; } = "";
    public int TotalEmployees { get; init; }
    public int NotStarted { get; init; }
    public int InProgress { get; init; }
    public int Complete { get; init; }
    /// <summary>Prêtes (commune validée + cellule complète) pas encore soumises au workflow.</summary>
    public int ReadyCount { get; init; }
    /// <summary>Fiches soumises au workflow (<c>Pending</c> chez le référent technique).</summary>
    public int SubmittedForValidationCount { get; init; }
    /// <summary>Total prêtes + soumises (rétrocompatibilité).</summary>
    public int ReadyForValidation { get; init; }
    /// <summary>Statut brouillon partie commune pour la cellule / période (Draft | Validated).</summary>
    public string? CommonPartStatus { get; init; }
    /// <summary>Done | InProgress | NotStarted | Empty</summary>
    public string ServiceAggregateState { get; init; } = "";
    /// <summary>Brouillon cellule (partie commune) le plus récent pour cette cellule et cette période — même lien que la saisie RACC/SAV.</summary>
    public Guid? LinkedCellulePrimeDraftId { get; init; }
    public string? LinkedTemplateId { get; init; }
    public string? LinkedTemplateDisplayName { get; init; }
    public bool PoolDistributionUnlocked { get; init; }
}
