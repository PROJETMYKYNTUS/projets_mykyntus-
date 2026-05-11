namespace PrimeBackend.Dto;

public sealed class CellulePrimeIndicatorDto
{
    public Guid Id { get; init; }
    public string CelluleId { get; init; } = "";
    public int SortOrder { get; init; }
    public string Label { get; init; } = "";
    public decimal? PonderationPrimePct { get; init; }
    public decimal? PonderationChallengePct { get; init; }
    public bool IsActive { get; init; }
    public string? TemplateStableId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

public sealed class PutCellulePrimeIndicatorsRequest
{
    public List<PutCellulePrimeIndicatorItem> Indicators { get; set; } = [];
}

public sealed class PutCellulePrimeIndicatorItem
{
    public int SortOrder { get; set; }
    public string Label { get; set; } = "";
    public decimal? PonderationPrimePct { get; set; }
    public decimal? PonderationChallengePct { get; set; }
    public bool IsActive { get; set; } = true;
    public string? TemplateStableId { get; set; }
}

public sealed class SupervisorPolePrimeDraftResponseDto
{
    public Guid Id { get; init; }
    public string SupervisorUserId { get; init; } = "";
    public string PoleId { get; init; } = "";
    public string Period { get; init; } = "";
    public string TemplateId { get; init; } = "";
    public string TemplateDisplayName { get; init; } = "";
    public int TemplateFormatVersion { get; init; }
    public string Status { get; init; } = "";
    public string SchemaJson { get; init; } = "{}";
    public string PoleSaisieJson { get; init; } = "{}";
    public string? ComputedJson { get; init; }
    public string? TemplateCalcSnapshotJson { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class UpsertSupervisorPolePrimeDraftRequest
{
    public string SupervisorUserId { get; set; } = "";
    public string PoleId { get; set; } = "";
    public string Period { get; set; } = "";
    public string TemplateId { get; set; } = "";
    public string TemplateDisplayName { get; set; } = "";
    public int TemplateFormatVersion { get; set; }
    public string SchemaJson { get; set; } = "{}";
    public string PoleSaisieJson { get; set; } = "{}";
    public string? ComputedJson { get; set; }
    public string? TemplateCalcSnapshotJson { get; set; }
    public string? Status { get; set; }
}

public sealed class EmployeePrimeCellFicheResponseDto
{
    public Guid Id { get; init; }
    public Guid PolePrimeDraftId { get; init; }
    public string SupervisorUserId { get; init; } = "";
    public string EmployeeId { get; init; } = "";
    public string CelluleId { get; init; } = "";
    public string PoleId { get; init; } = "";
    public string Period { get; init; } = "";
    public string CellSaisieJson { get; init; } = "{}";
    public string FillingStatus { get; init; } = "";
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class UpsertEmployeePrimeCellFicheRequest
{
    public string SupervisorUserId { get; set; } = "";
    public string EmployeeId { get; set; } = "";
    public string Period { get; set; } = "";
    public Guid PolePrimeDraftId { get; set; }
    public string CellSaisieJson { get; set; } = "{}";
}

public sealed class EmployeePrimeCellFicheListItemDto
{
    public string EmployeeId { get; init; } = "";
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string Email { get; init; } = "";
    public string CelluleId { get; init; } = "";
    public Guid? FicheId { get; init; }
    public Guid? PolePrimeDraftId { get; init; }
    public string FillingStatus { get; init; } = "NotStarted";
    public string CellSaisieJson { get; init; } = "{}";
    public DateTimeOffset? UpdatedAt { get; init; }
}

public sealed class CellPilotageSummaryDto
{
    public string CelluleId { get; init; } = "";
    public string CelluleName { get; init; } = "";
    public string PoleId { get; init; } = "";
    public int TotalEmployees { get; init; }
    public int NotStarted { get; init; }
    public int InProgress { get; init; }
    public int Complete { get; init; }
    /// <summary>Done | InProgress | NotStarted | Empty</summary>
    public string CellAggregateState { get; init; } = "";
    /// <summary>Brouillon pôle (partie commune) le plus récent pour ce pôle et cette période — même lien que la saisie RACC/SAV.</summary>
    public Guid? LinkedPolePrimeDraftId { get; init; }
    public string? LinkedTemplateId { get; init; }
    public string? LinkedTemplateDisplayName { get; init; }
}
