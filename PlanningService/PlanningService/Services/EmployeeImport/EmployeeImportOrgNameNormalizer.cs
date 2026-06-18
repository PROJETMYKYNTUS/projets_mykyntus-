namespace PlanningService.Services.EmployeeImport;

/// <summary>
/// Normalisation des noms d'organisation à l'import (retrait préfixes métier redondants).
/// </summary>
public static class EmployeeImportOrgNameNormalizer
{
    private static readonly Dictionary<string, string[]> LevelPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pole"] = ["departement", "department", "etage", "pole", "pôle"],
        ["cellule"] = ["equipe mere", "équipe mère", "unite", "unité", "cellule", "cell"],
        ["service"] = ["sous service", "sous-service", "sub service", "subservice", "equipe", "équipe", "service"],
    };

    public static string StripLevelPrefix(string? raw, string fieldKey)
    {
        var normalized = EmployeeImportColumnMatcher.Normalize(raw ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
            return normalized;

        if (!LevelPrefixes.TryGetValue(fieldKey, out var prefixes))
            return normalized;

        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var changed = true;

        while (changed && tokens.Count > 0)
        {
            changed = false;
            foreach (var prefix in prefixes.OrderByDescending(p => p.Split(' ').Length))
            {
                var prefixTokens = prefix.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Count < prefixTokens.Length)
                    continue;

                if (tokens.Take(prefixTokens.Length).SequenceEqual(prefixTokens, StringComparer.OrdinalIgnoreCase))
                {
                    tokens.RemoveRange(0, prefixTokens.Length);
                    changed = true;
                    break;
                }
            }
        }

        return tokens.Count == 0 ? normalized : string.Join(' ', tokens);
    }

    public static bool ContainsAllTokens(string longerNormalized, string shorterNormalized)
    {
        var longTokens = TokenSet(longerNormalized);
        var shortTokens = TokenSet(shorterNormalized);
        if (shortTokens.Count == 0)
            return false;

        return shortTokens.All(longTokens.Contains);
    }

    public static HashSet<string> TokenSet(string normalized) =>
        normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
