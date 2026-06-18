using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DocumentationBackend.Services;

public interface ITemplatePlaceholderNormalizationService
{
    IReadOnlyList<NormalizedPlaceholderMatch> ExtractPlaceholders(string content);
    string NormalizeKey(string raw);
    string ResolveCanonicalKey(string normalizedKey);
    string BuildDisplayLabel(string canonicalKey);
    bool IsDatabaseBackedKey(string canonicalKey);
}

public sealed record NormalizedPlaceholderMatch(
    string RawToken,
    string NormalizedKey,
    string CanonicalKey,
    string Status,
    string SuggestedLabel,
    string Type,
    bool IsRequired,
    string? ValidationRule);

public sealed class TemplatePlaceholderNormalizationService(IRibValidationService ribValidation)
    : ITemplatePlaceholderNormalizationService
{
    private static readonly Regex CurlyPlaceholderRegex = new(@"\{\{\s*([^{}]+?)\s*\}\}", RegexOptions.Compiled);
    private static readonly Regex HumanPlaceholderRegex = new(@"\(([^\(\)\r\n]{1,120})\)", RegexOptions.Compiled);
    /// <summary>Ignore uniquement les jetons vides / blancs — plus de filtrage silencieux de <c>(X)</c>.</summary>
    private static readonly Regex InvalidTokenRegex = new(@"^\s+$", RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, string> Synonyms =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["nom"] = "nom_employe",
            ["prenom"] = "prenom_employe",
            ["nom_employe"] = "nom_employe",
            ["employe_nom"] = "nom_employe",
            ["prenom_employe"] = "prenom_employe",
            ["employe_prenom"] = "prenom_employe",
            ["nom_complet"] = "nom_complet",
            ["nom_pilote"] = "nom_complet",
            ["nom_collaborateur"] = "nom_complet",
            ["cin"] = "numero_cin",
            ["cin_nr"] = "numero_cin",
            ["nr_cin"] = "numero_cin",
            ["cin_numero"] = "numero_cin",
            ["cni"] = "numero_cin",
            ["numero_cin"] = "numero_cin",
            ["salaire"] = "salaire_brut",
            ["salaire_net"] = "salaire_brut",
            ["salaire_brut"] = "salaire_brut",
            ["date_embauche"] = "date_embauche",
            ["poste"] = "poste",
            ["qualite"] = "poste",
            ["implique"] = "poste",
            ["fonction"] = "poste",
            ["role_employe"] = "role",
            ["email"] = "email",
            ["mail"] = "email",
            ["telephone"] = "telephone",
            ["tel"] = "telephone",
            ["rib"] = "rib",
            ["iban"] = "rib",
            ["compte_bancaire"] = "rib",
            ["xxxx"] = "rib",
            ["date_document"] = "date_document",
            ["date"] = "date_document",
            ["departement"] = "departement",
            ["service"] = "service",
            ["cellule"] = "cellule",
            ["pole"] = "pole",
            ["motif"] = "motif",
        };

    private static readonly HashSet<string> DatabaseBackedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "nom",
        "prenom",
        "nom_employe",
        "prenom_employe",
        "nom_complet",
        "prenom_nom",
        "email",
        "pole",
        "cellule",
        "departement",
        "date_document",
        "date",
        "date_effet",
    };

    public IReadOnlyList<NormalizedPlaceholderMatch> ExtractPlaceholders(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Array.Empty<NormalizedPlaceholderMatch>();

        var results = new Dictionary<string, NormalizedPlaceholderMatch>(StringComparer.OrdinalIgnoreCase);

        AddMatches(results, CurlyPlaceholderRegex.Matches(content).Select(m => m.Groups[1].Value.Trim()), wrapWithParens: false);
        AddMatches(results, HumanPlaceholderRegex.Matches(content).Select(m => m.Groups[1].Value.Trim()), wrapWithParens: true);

        return results.Values
            .OrderBy(x => x.CanonicalKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.RawToken, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string NormalizeKey(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var decomposed = raw.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        var previousUnderscore = false;
        foreach (var ch in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
                previousUnderscore = false;
                continue;
            }

            if (ch is ' ' or '-' or '_' or '/')
            {
                if (!previousUnderscore && sb.Length > 0)
                {
                    sb.Append('_');
                    previousUnderscore = true;
                }
            }
        }

        return sb.ToString().Trim('_');
    }

    public string ResolveCanonicalKey(string normalizedKey)
    {
        if (string.IsNullOrWhiteSpace(normalizedKey))
            return string.Empty;
        return Synonyms.TryGetValue(normalizedKey.Trim(), out var mapped)
            ? mapped
            : normalizedKey.Trim();
    }

    public string BuildDisplayLabel(string canonicalKey)
    {
        if (string.IsNullOrWhiteSpace(canonicalKey))
            return string.Empty;

        var label = canonicalKey.Trim().Replace("_", " ", StringComparison.Ordinal);
        return CultureInfo.GetCultureInfo("fr-FR").TextInfo.ToTitleCase(label);
    }

    public bool IsDatabaseBackedKey(string canonicalKey) =>
        !string.IsNullOrWhiteSpace(canonicalKey) && DatabaseBackedKeys.Contains(canonicalKey.Trim());

    private void AddMatches(
        Dictionary<string, NormalizedPlaceholderMatch> results,
        IEnumerable<string> rawMatches,
        bool wrapWithParens)
    {
        foreach (var raw in rawMatches)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            if (InvalidTokenRegex.IsMatch(raw))
                continue;

            var normalized = NormalizeKey(raw);
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            var canonical = ResolveCanonicalKey(normalized);
            var key = $"{canonical}|{raw}";
            if (results.ContainsKey(key))
                continue;

            var type = InferVariableType(canonical);
            var validationRule = InferValidationRule(canonical, type);
            var status = string.Equals(normalized, canonical, StringComparison.Ordinal) ? "resolved" : "mapped";
            results[key] = new NormalizedPlaceholderMatch(
                wrapWithParens ? $"({raw})" : "{{" + "{" + raw + "}" + "}",
                normalized,
                canonical,
                status,
                BuildDisplayLabel(canonical),
                type,
                IsRequired(canonical),
                validationRule);
        }
    }

    private static bool IsRequired(string canonicalKey) =>
        !canonicalKey.StartsWith("opt_", StringComparison.OrdinalIgnoreCase)
        && !canonicalKey.Contains("facultatif", StringComparison.OrdinalIgnoreCase);

    private static string InferVariableType(string lower)
    {
        if (ContainsAny(lower, "date", "naissance", "embauche", "echeance"))
            return "date";
        if (ContainsAny(lower, "salaire", "montant", "brut", "net", "prime", "indemnite", "taux"))
            return "number";
        return "text";
    }

    private string? InferValidationRule(string lower, string type)
    {
        if (lower.Contains("cin", StringComparison.Ordinal))
            return @"^[A-Za-z0-9]{4,20}$";
        if (ContainsAny(lower, "rib", "iban", "compte"))
            return ribValidation.DigitsOnlyValidationPattern;
        if (type == "date")
            return @"^\d{4}-\d{2}-\d{2}$|^\d{2}/\d{2}/\d{4}$";
        return null;
    }

    private static bool ContainsAny(string lower, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (lower.Contains(needle, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
