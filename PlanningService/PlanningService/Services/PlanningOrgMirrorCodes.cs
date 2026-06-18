namespace PlanningService.Services;

/// <summary>
/// Codes courts pour le miroir org Planning (colonne <c>Code</c> limitée à 20 caractères).
/// </summary>
public static class PlanningOrgMirrorCodes
{
    private const int MaxLength = 20;

    public static string ForCellule(string externalId) => Build("C", externalId);

    public static string ForLeafService(string externalId) => Build("S", externalId);

    private static string Build(string prefix, string externalId)
    {
        var compact = externalId
            .Replace("-", "", StringComparison.Ordinal)
            .ToUpperInvariant();

        var budget = MaxLength - prefix.Length;
        if (budget <= 0)
            return prefix[..MaxLength];

        if (compact.Length <= budget)
            return prefix + compact;

        return prefix + compact[..budget];
    }
}
