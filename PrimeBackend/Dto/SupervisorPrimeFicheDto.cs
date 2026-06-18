namespace PrimeBackend.Dto;

public sealed class SupervisorPrimeFicheResponseDto
{
    public Guid Id { get; init; }
    public string SupervisorUserId { get; init; } = "";
    public string? CelluleId { get; init; }
    public string Period { get; init; } = "";
    public string TemplateId { get; init; } = "";
    public string TemplateDisplayName { get; init; } = "";
    public int TemplateFormatVersion { get; init; }
    public string Status { get; init; } = "";
    public string SchemaJson { get; init; } = "{}";
    public string SaisieJson { get; init; } = "{}";
    public string? ComputedJson { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ValidatedAt { get; init; }
}

public sealed class CreateSupervisorPrimeFicheRequest
{
    public string SupervisorUserId { get; set; } = "";
    public string? CelluleId { get; set; }
    public string Period { get; set; } = "";
    public string TemplateId { get; set; } = "";
    public string TemplateDisplayName { get; set; } = "";
    public int TemplateFormatVersion { get; set; }
    public string SchemaJson { get; set; } = "{}";
    public string SaisieJson { get; set; } = "{}";
    public string? ComputedJson { get; set; }
}

public sealed class UpdateSupervisorPrimeFicheSaisieRequest
{
    public string SaisieJson { get; set; } = "{}";
    public string? ComputedJson { get; set; }
}
