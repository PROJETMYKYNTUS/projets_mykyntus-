namespace DocumentationBackend.Configuration;

/// <summary>Validation centralisée du RIB marocain (chiffres uniquement, longueur configurable).</summary>
public sealed class RibValidationOptions
{
    public const string SectionName = "RibValidation";

    /// <summary>Nombre minimal de chiffres (inclus).</summary>
    public int MinDigits { get; set; } = 14;

    /// <summary>Nombre maximal de chiffres (inclus).</summary>
    public int MaxDigits { get; set; } = 24;
}
