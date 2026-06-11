namespace PrimeBackend.Dto;

/// <summary>Références backend à un template fiche PRIME (id local navigateur ou import).</summary>
public sealed class PrimeFicheTemplateUsageDto
{
    public string TemplateId { get; init; } = "";
    public int CommonsDraftCount { get; init; }
    public int PilotFicheCount { get; init; }
    public int FrozenPilotFicheCount { get; init; }
    public int ValidatedPilotFicheCount { get; init; }
    public int TotalReferenceCount => CommonsDraftCount + PilotFicheCount;
    /// <summary>Suppression définitive autorisée uniquement sans référence en base ni snapshot figé.</summary>
    public bool CanHardDelete =>
        TotalReferenceCount == 0 && FrozenPilotFicheCount == 0;
    /// <summary>hardDelete | archive</summary>
    public string RecommendedAction =>
        CanHardDelete ? "hardDelete" : "archive";
}

/// <summary>Vérification d’unicité du nom affiché (brouillons superviseur en base).</summary>
public sealed class PrimeFicheTemplateDisplayNameCheckDto
{
    public string DisplayName { get; init; } = "";
    public bool Taken { get; init; }
}
