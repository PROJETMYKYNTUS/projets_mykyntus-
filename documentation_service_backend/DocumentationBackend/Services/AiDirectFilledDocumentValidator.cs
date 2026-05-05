using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DocumentationBackend.Services;

/// <summary>
/// Contrôles post-IA : placeholders résiduels et présence dans le texte final des valeurs fournies (données critiques).
/// Aucun remplacement local — uniquement validation.
/// </summary>
public static class AiDirectFilledDocumentValidator
{
    private static readonly Regex[] PlaceholderPatterns =
    {
        new(@"\([Xx]\)", RegexOptions.Compiled),
        new(@"\(\s*\)", RegexOptions.Compiled),
        new(@"\(\s+\)", RegexOptions.Compiled),
        new(@"\(XXXX\)", RegexOptions.Compiled),
        new(@"\{\{\s*[a-zA-Z0-9_]+\s*\}\}", RegexOptions.Compiled),
        new(@"XX/XX/\d{4}", RegexOptions.Compiled),
        new(@"_{3,}", RegexOptions.Compiled),
        new(@"-{4,}", RegexOptions.Compiled),
    };

    /// <summary>Clés métier par défaut : si une valeur non vide existe dans les données fusionnées, elle doit apparaître dans le document final.</summary>
    private static readonly HashSet<string> DefaultCriticalKeySuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "nom_complet",
        "nom",
        "prenom",
        "cin",
        "date",
        "date_document",
        "date_effet",
    };

    /// <summary>Résidu minimal demandé par l’UI « Génération de documents » (sans expressions régulières).</summary>
    public static IReadOnlyList<string> FindUserListedResidualPlaceholders(string document)
    {
        if (string.IsNullOrEmpty(document))
            return Array.Empty<string>();

        var hits = new List<string>();
        if (document.Contains("(X)", StringComparison.OrdinalIgnoreCase))
            hits.Add("(X)");
        if (document.Contains("()", StringComparison.Ordinal))
            hits.Add("()");
        if (document.Contains("( )", StringComparison.Ordinal))
            hits.Add("( )");
        if (document.Contains("___", StringComparison.Ordinal))
            hits.Add("___");
        if (document.Contains("----", StringComparison.Ordinal))
            hits.Add("----");
        return hits.Distinct(StringComparer.Ordinal).ToList();
    }

    public static IReadOnlyList<string> FindRemainingPlaceholders(string document)
    {
        if (string.IsNullOrEmpty(document))
            return Array.Empty<string>();

        var hits = new List<string>();
        foreach (var rx in PlaceholderPatterns)
        {
            foreach (Match m in rx.Matches(document))
            {
                if (m.Success && !string.IsNullOrEmpty(m.Value))
                    hits.Add(m.Value);
            }
        }

        return hits.Distinct(StringComparer.Ordinal).Take(50).ToList();
    }

    public static IReadOnlyDictionary<string, string> MergeFlattenedData(JsonElement dbData, JsonElement formData)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        FlattenInto(dbData, "", merged, overwrite: false);
        FlattenInto(formData, "", merged, overwrite: true);
        return merged;
    }

    /// <summary>
    /// Pour chaque entrée fusionnée dont la clé correspond à une donnée critique par défaut,
    /// vérifie que la valeur (normalisée) est retrouvable dans le document généré.
    /// </summary>
    public static IReadOnlyList<string> FindCriticalDataNotReflectedInOutput(
        IReadOnlyDictionary<string, string> mergedFlat,
        string output)
    {
        if (string.IsNullOrEmpty(output) || mergedFlat.Count == 0)
            return Array.Empty<string>();

        var failures = new List<string>();
        var normOut = CollapseWs(output);

        foreach (var kv in mergedFlat)
        {
            if (!IsCriticalKey(kv.Key))
                continue;
            var val = (kv.Value ?? "").Trim();
            if (val.Length == 0)
                continue;

            if (ValueAppearsInOutput(val, normOut))
                continue;

            failures.Add($"Donnée critique non reflétée dans le document : « {kv.Key} ».");
        }

        return failures;
    }

    private static bool IsCriticalKey(string dottedKey)
    {
        var leaf = dottedKey;
        var lastDot = dottedKey.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < dottedKey.Length - 1)
            leaf = dottedKey[(lastDot + 1)..];
        var lastBracket = leaf.LastIndexOf('[');
        if (lastBracket >= 0 && lastBracket < leaf.Length - 1)
            leaf = leaf[..lastBracket];

        return DefaultCriticalKeySuffixes.Contains(leaf);
    }

    private static bool ValueAppearsInOutput(string rawValue, string normOutput)
    {
        var v = CollapseWs(rawValue);
        if (v.Length == 0)
            return true;

        if (normOutput.Contains(v, StringComparison.OrdinalIgnoreCase))
            return true;

        if (DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dIso)
            || DateTime.TryParse(v, CultureInfo.GetCultureInfo("fr-FR"), DateTimeStyles.None, out dIso))
        {
            var fr = dIso.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("fr-FR"));
            if (normOutput.Contains(fr, StringComparison.Ordinal))
                return true;
            var iso = dIso.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (normOutput.Contains(iso, StringComparison.Ordinal))
                return true;
        }

        foreach (var token in v.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length >= 3 && normOutput.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string CollapseWs(string s) =>
        Regex.Replace(s.Trim(), @"\s+", " ", RegexOptions.Compiled);

    private static void FlattenInto(JsonElement el, string prefix, Dictionary<string, string> target, bool overwrite)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in el.EnumerateObject())
                {
                    var next = string.IsNullOrEmpty(prefix) ? p.Name : $"{prefix}.{p.Name}";
                    FlattenInto(p.Value, next, target, overwrite);
                }

                break;
            case JsonValueKind.Array:
            {
                var i = 0;
                foreach (var item in el.EnumerateArray())
                {
                    FlattenInto(item, $"{prefix}[{i}]", target, overwrite);
                    i++;
                }

                break;
            }
            case JsonValueKind.String:
                PutScalar(prefix, el.GetString() ?? "", target, overwrite);
                break;
            case JsonValueKind.Number:
                PutScalar(prefix, el.GetRawText(), target, overwrite);
                break;
            case JsonValueKind.True:
                PutScalar(prefix, "true", target, overwrite);
                break;
            case JsonValueKind.False:
                PutScalar(prefix, "false", target, overwrite);
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                break;
            default:
                PutScalar(prefix, el.GetRawText(), target, overwrite);
                break;
        }
    }

    private static void PutScalar(string key, string value, Dictionary<string, string> target, bool overwrite)
    {
        if (string.IsNullOrEmpty(key))
            return;
        if (!overwrite && target.ContainsKey(key))
            return;
        target[key] = value;
    }
}
