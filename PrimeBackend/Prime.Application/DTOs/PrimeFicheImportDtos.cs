namespace Prime.Application.DTOs;

public sealed class ImportReadyFicheRequest
{
    public string SupervisorUserId { get; set; } = "";
    public string Period { get; set; } = "";
    public string CelluleId { get; set; } = "";
    public string? EmployeeId { get; set; }
    public string? EmployeeExternalName { get; set; }
    public bool IsHistorical { get; set; }
    public string OriginFileName { get; set; } = "";
    public string? PreviewSheetName { get; set; }
    public string? TemplateVersionRef { get; set; }
    public List<List<string>> Rows { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public decimal? PrimeAmount { get; set; }
    public decimal? ChallengeAmount { get; set; }
    public decimal? TotalAmount { get; set; }
    public string ServiceSaisieJson { get; set; } = "{}";
}

public sealed class ImportReadyFicheResponseDto
{
    public string Outcome { get; init; } = "";
    public Guid? FicheId { get; init; }
    public Guid? HistoricalFicheId { get; init; }
    public string? EmployeeId { get; init; }
    public string? EmployeeDisplayName { get; init; }
    public string Period { get; init; } = "";
    public string ValidationStatus { get; init; } = "";
    public DateTimeOffset ImportedAt { get; init; }
}

public sealed class PrimeHistoricalFicheListItemDto
{
    public Guid Id { get; init; }
    public string Period { get; init; } = "";
    public string CelluleId { get; init; } = "";
    public string? ServiceId { get; init; }
    public string EmployeeExternalName { get; init; } = "";
    public string? EmployeeId { get; init; }
    public decimal? PrimeAmount { get; init; }
    public decimal? ChallengeAmount { get; init; }
    public decimal? TotalAmount { get; init; }
    public string OriginFileName { get; init; } = "";
    public string Source { get; init; } = "";
    public DateTimeOffset ImportedAt { get; init; }
    public bool HasDetailGrid { get; init; }
}

/// <summary>Grille détaillée figée d'une archive historique (import sans employé reconnu).</summary>
public sealed class PrimeHistoricalFicheDetailSnapshotDto
{
    public Guid HistoricalFicheId { get; init; }
    public int Version { get; init; }
    public string? PreviewSheetName { get; init; }
    public string? TemplateVersionRef { get; init; }
    public List<List<string>> Rows { get; init; } = [];
    public List<string> Errors { get; init; } = [];
    public decimal? PrimeAmount { get; init; }
    public decimal? ChallengeAmount { get; init; }
    public decimal? TotalAmount { get; init; }
    public string EmployeeExternalName { get; init; } = "";
    public string Period { get; init; } = "";
    public string OriginFileName { get; init; } = "";
    public DateTimeOffset ImportedAt { get; init; }
}
