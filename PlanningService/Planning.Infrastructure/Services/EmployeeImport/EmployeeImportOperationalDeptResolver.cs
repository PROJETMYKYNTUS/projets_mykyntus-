using Planning.Infrastructure.Services;

namespace Planning.Infrastructure.Services.EmployeeImport;

public static class EmployeeImportOperationalDeptResolver
{
    public static Guid? ResolveBusinessDepartmentId(
        string? operationalDepartmentName,
        IReadOnlyList<DirectoryOperationalDepartmentJson> departments)
    {
        if (string.IsNullOrWhiteSpace(operationalDepartmentName) || departments.Count == 0)
            return null;

        var normalized = EmployeeImportColumnMatcher.Normalize(operationalDepartmentName);
        var matches = departments.Where(d =>
            EmployeeImportColumnMatcher.Normalize(d.Name) == normalized
            || EmployeeImportColumnMatcher.Normalize(d.Code) == normalized
            || EmployeeImportColumnMatcher.Normalize($"{d.Code} {d.Name}") == normalized
            || EmployeeImportColumnMatcher.Normalize($"{d.Name} {d.Code}") == normalized
            || EmployeeImportColumnMatcher.Normalize($"{d.Code} - {d.Name}") == normalized).ToList();

        if (matches.Count == 1 && Guid.TryParse(matches[0].Id, out var single))
            return single;

        var codePrefix = TryExtractLeadingCode(normalized);
        if (codePrefix is not null)
        {
            matches = departments.Where(d =>
                EmployeeImportColumnMatcher.Normalize(d.Code) == codePrefix).ToList();
            if (matches.Count == 1 && Guid.TryParse(matches[0].Id, out var byCode))
                return byCode;
        }

        // Repli tolérant : la cellule contient (ou approche) le nom du département, même si le code
        // est absent ou erroné (ex. « OP-003 - departement operationnel » alors que seul OP-001 existe).
        // On réutilise le même score inclusion + Levenshtein que la détection des en-têtes, et on
        // n'accepte que si le meilleur candidat est sans ambiguïté.
        var fuzzy = ResolveByTolerance(normalized, departments);
        if (fuzzy is not null && Guid.TryParse(fuzzy.Id, out var byName))
            return byName;

        return null;
    }

    /// <summary>
    /// Décompose une valeur fichier (ex. « OP-001 », « OP-001 - Contact centre ») en code + nom
    /// pour la création Directory.
    /// </summary>
    public static (string? Code, string Name) ParseCodeAndName(string raw)
    {
        var trimmed = raw.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return (null, string.Empty);

        var match = System.Text.RegularExpressions.Regex.Match(
            trimmed,
            @"^(?<code>OP[\s\-]?\d+)\s*[-–:]?\s*(?<name>.*)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        if (!match.Success)
            return (null, trimmed);

        var codeRaw = match.Groups["code"].Value.Trim();
        var code = System.Text.RegularExpressions.Regex.Replace(
            codeRaw,
            @"^(OP)[\s\-]?(\d+)$",
            "OP-$2",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).ToUpperInvariant();
        var namePart = match.Groups["name"].Value.Trim();
        return (code, string.IsNullOrWhiteSpace(namePart) ? code : namePart);
    }

    /// <summary>Seuil d'acceptation de la correspondance tolérante (aligné sur la détection d'en-têtes).</summary>
    private const double ToleranceThreshold = 0.75;

    private static DirectoryOperationalDepartmentJson? ResolveByTolerance(
        string normalizedInput,
        IReadOnlyList<DirectoryOperationalDepartmentJson> departments)
    {
        var scored = departments
            .Select(d => new
            {
                Dept = d,
                Score = Math.Max(
                    EmployeeImportColumnMatcher.SimilarityScore(
                        normalizedInput, EmployeeImportColumnMatcher.Normalize(d.Name)),
                    EmployeeImportColumnMatcher.SimilarityScore(
                        normalizedInput, EmployeeImportColumnMatcher.Normalize($"{d.Code} {d.Name}")))
            })
            .Where(x => x.Score >= ToleranceThreshold)
            .OrderByDescending(x => x.Score)
            .ToList();

        if (scored.Count == 0)
            return null;

        // Un seul candidat, ou un meilleur nettement détaché du suivant → non ambigu.
        if (scored.Count == 1 || scored[0].Score - scored[1].Score >= 0.05)
            return scored[0].Dept;

        return null;
    }

    private static string? TryExtractLeadingCode(string normalizedInput)
    {
        // « op 001 departement operationnel » → « op 001 »
        var parts = normalizedInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && parts[0].Length <= 4 && parts[1].All(char.IsDigit))
            return $"{parts[0]} {parts[1]}";
        return parts.Length >= 1 ? parts[0] : null;
    }

    public static Guid? ResolveUniqueByPoleName(
        string poleName,
        IReadOnlyList<DirectoryOperationalDepartmentJson> departments,
        IReadOnlyList<EmployeeImportOperationalPoleRef> poles)
    {
        if (string.IsNullOrWhiteSpace(poleName) || poles.Count == 0)
            return null;

        var normalized = EmployeeImportColumnMatcher.Normalize(poleName);
        var matches = poles
            .Where(p => EmployeeImportColumnMatcher.Normalize(p.Name) == normalized)
            .Select(p => p.BusinessDepartmentId)
            .Distinct()
            .ToList();

        if (matches.Count != 1 || !Guid.TryParse(matches[0], out var deptId))
            return null;

        return departments.Any(d => d.Id == matches[0]) ? deptId : null;
    }
}

public sealed class EmployeeImportOperationalPoleRef
{
    public string Name { get; init; } = "";
    public string BusinessDepartmentId { get; init; } = "";
}
