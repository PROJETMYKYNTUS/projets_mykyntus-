namespace PrimeBackend.Dto;

/// <summary>Détail des fiches pilotes impactées par une tentative de suppression.</summary>
public sealed class DeletionImpactDto
{
    public int TotalPilotCount { get; init; }
    public int DeletablePilotCount { get; init; }
    public int BlockedPilotCount { get; init; }
    public int FrozenCount { get; init; }
    public int InWorkflowCount { get; init; }
    public int TerminalCount { get; init; }
    public bool HasGlobalPool { get; init; }
}

public sealed class CommonsDraftDeletionCheckDto
{
    public Guid DraftId { get; init; }
    public bool CanDelete { get; init; }
    public string? Reason { get; init; }
    public DeletionImpactDto Impact { get; init; } = new();
}

public sealed class PilotFicheDeletionCheckDto
{
    public Guid FicheId { get; init; }
    public bool CanDelete { get; init; }
    public string? Reason { get; init; }
}
