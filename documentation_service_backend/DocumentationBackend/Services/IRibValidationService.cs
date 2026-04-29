namespace DocumentationBackend.Services;

/// <summary>Règle métier unique pour le RIB (chiffres, longueur bornée).</summary>
public interface IRibValidationService
{
    /// <summary>Extrait les chiffres et vérifie la longueur [MinDigits, MaxDigits].</summary>
    bool IsValidRibDigits(string? raw);

    /// <summary>Motif <see cref="Regex"/> pour variables de type RIB (chiffres uniquement).</summary>
    string DigitsOnlyValidationPattern { get; }

    static bool IsRibLikeVariableName(string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
            return false;
        var k = variableName.Trim().ToLowerInvariant();
        return k.Contains("rib", StringComparison.Ordinal)
               || k.Contains("iban", StringComparison.Ordinal)
               || k.Contains("compte", StringComparison.Ordinal);
    }
}
