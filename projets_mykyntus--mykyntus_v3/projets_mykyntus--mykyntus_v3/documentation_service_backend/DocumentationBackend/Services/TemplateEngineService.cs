using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentationBackend.Application.Abstractions;
using DocumentationBackend.Infrastructure.Text;

namespace DocumentationBackend.Services;

public interface ITemplateEngineService
{
    IReadOnlyList<DetectedTemplateVariable> DetectVariables(string content);
    string BuildRuleBasedContent(string description, IReadOnlyList<string> variableNames);
    string RenderContent(string structuredContent, IReadOnlyDictionary<string, string> values);

    /// <summary>Valide les valeurs fournies par rapport aux variables détectées (obligatoires et règles regex).</summary>
    VariableValidationResult ValidateValues(
        IReadOnlyList<DetectedTemplateVariable> variables,
        IReadOnlyDictionary<string, string> values);

    /// <summary>Alias explicite métier de <see cref="ValidateValues"/> (contrat stable côté workflow).</summary>
    VariableValidationResult ValidateVariables(
        IReadOnlyList<DetectedTemplateVariable> variables,
        IReadOnlyDictionary<string, string> values);

    /// <summary>Fusionne les variables regex locales avec l’analyse IA (enrichissement types, règles, descriptions).</summary>
    IReadOnlyList<DetectedTemplateVariable> MergeWithAiDetected(
        IReadOnlyList<DetectedTemplateVariable> regexVars,
        IReadOnlyList<AiDetectedVariable> aiVars);

    /// <summary>
    /// Après rendu strict, liste les marqueurs encore présents ((X), {{var}}, masques date, etc.).
    /// Utilisé pour bloquer la génération tant que le document n’est pas entièrement résolu.
    /// </summary>
    IReadOnlyList<string> ListStructuralResidualsAfterRender(
        string structuredContent,
        IReadOnlyDictionary<string, string> values);
}

public sealed record DetectedTemplateVariable(
    string Name,
    string Type,
    bool IsRequired,
    string? ValidationRule,
    string? Description = null);

/// <summary>Résultat de la validation des variables de modèle.</summary>
public sealed record VariableValidationResult(
    bool IsValid,
    IReadOnlyList<string> MissingRequired,
    IReadOnlyList<string> InvalidFormat);

public sealed class TemplateEngineService(
    ITemplatePlaceholderNormalizationService placeholderNormalization,
    IRibValidationService ribValidation) : ITemplateEngineService
{
    private static readonly Regex HumanPlaceholderRegex = new(@"\(([^\(\)\r\n]{1,120})\)", RegexOptions.Compiled);

    private static readonly Regex LegacyDateMaskRegex = new(@"XX/XX/20\d{2}", RegexOptions.Compiled);

    private static readonly Regex IsoDateYyyyMmDdRegex = new(
        @"^\d{4}-\d{2}-\d{2}$",
        RegexOptions.Compiled);

    private static readonly Regex CinPlaceholderRegex = new(
        @"(CIN\s*(?:n[°ºo]\s*)?)(pilote|test|demo|xxx|—|-|n/?a)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RibPlaceholderRegex = new(
        @"(compte\s+bancaire\s*(?:n[°ºo]\s*)?)(—|-|xxx|test|n/?a)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CinValueRegex = new(
        @"^[A-Za-z]{1,2}[0-9]{4,8}$|^[0-9A-Za-z]{4,20}$",
        RegexOptions.Compiled);

    private static readonly Regex MultiSpaceRegex = new(@"[ \t]{2,}", RegexOptions.Compiled);

    private static readonly Regex MissingSentenceSpaceRegex = new(
        @"([^\s])([:;!?])([^\s])",
        RegexOptions.Compiled);

    public IReadOnlyList<DetectedTemplateVariable> DetectVariables(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Array.Empty<DetectedTemplateVariable>();

        var names = placeholderNormalization.ExtractPlaceholders(content)
            .Select(m => m.CanonicalKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return names.Select(BuildDetectedVariable).ToList();
    }

    private DetectedTemplateVariable BuildDetectedVariable(string name)
    {
        var lower = name.ToLowerInvariant();
        var type = InferVariableType(lower);
        var validation = InferValidationRule(lower, type);
        var required = !name.StartsWith("opt_", StringComparison.OrdinalIgnoreCase)
                       && !lower.Contains("facultatif", StringComparison.Ordinal);
        return new DetectedTemplateVariable(name, type, required, validation, null);
    }

    /// <inheritdoc />
    public IReadOnlyList<DetectedTemplateVariable> MergeWithAiDetected(
        IReadOnlyList<DetectedTemplateVariable> regexVars,
        IReadOnlyList<AiDetectedVariable> aiVars)
    {
        var map = new Dictionary<string, DetectedTemplateVariable>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in regexVars)
            map[r.Name] = r;

        foreach (var ai in aiVars)
        {
            var key = ai.Name.Trim();
            if (string.IsNullOrEmpty(key))
                continue;
            var aiType = NormalizeMergedAiType(ai.Type);
            if (!map.TryGetValue(key, out var existing))
            {
                map[key] = new DetectedTemplateVariable(
                    key,
                    aiType,
                    ai.IsRequired,
                    NormalizeValidationRule(ai.ValidationRule),
                    string.IsNullOrWhiteSpace(ai.Description) ? null : ai.Description.Trim());
                continue;
            }

            var mergedType = RefineTypeWithAi(existing.Type, aiType);
            var mergedRule = string.IsNullOrWhiteSpace(existing.ValidationRule)
                ? NormalizeValidationRule(ai.ValidationRule)
                : existing.ValidationRule;
            var mergedDesc = string.IsNullOrWhiteSpace(ai.Description) ? existing.Description : ai.Description.Trim();
            var mergedReq = existing.IsRequired || ai.IsRequired;
            map[key] = new DetectedTemplateVariable(key, mergedType, mergedReq, mergedRule, mergedDesc);
        }

        return map.Values
            .OrderByDescending(v => v.IsRequired)
            .ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeMergedAiType(string t)
    {
        var s = (t ?? "text").Trim().ToLowerInvariant();
        return s switch
        {
            "date" or "number" or "email" or "phone" or "boolean" or "text" => s,
            _ => "text",
        };
    }

    private static string? NormalizeValidationRule(string? rule) =>
        string.IsNullOrWhiteSpace(rule) ? null : rule.Trim();

    private static string RefineTypeWithAi(string regexType, string aiType)
    {
        if (string.Equals(regexType, "text", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(aiType, "text", StringComparison.OrdinalIgnoreCase))
            return aiType;
        return regexType;
    }

    private static string InferVariableType(string lower)
    {
        if (ContainsAny(lower, "date", "naissance", "embauche", "expiration", "echeance", "mois"))
            return "date";
        if (ContainsAny(lower, "salaire", "montant", "prix", "brut", "net", "cotisation", "prime", "indemnite", "capital", "heures", "taux", "quantite"))
            return "number";
        if (ContainsAny(lower, "email", "courriel", "mail"))
            return "email";
        if (ContainsAny(lower, "tel", "phone", "portable", "gsm"))
            return "phone";
        if (ContainsAny(lower, "actif", "valide", "consent", "accord"))
            return "boolean";
        return "text";
    }

    private static bool ContainsAny(string lower, params string[] needles)
    {
        foreach (var n in needles)
        {
            if (lower.Contains(n, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private string? InferValidationRule(string lower, string type)
    {
        if (lower.Contains("cin", StringComparison.Ordinal))
            return @"^[A-Za-z0-9]{4,20}$";
        if (ContainsAny(lower, "rib", "iban", "compte"))
            return ribValidation.DigitsOnlyValidationPattern;
        if (type == "email")
            return @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        if (type == "phone")
            return @"^\+?[0-9\s\-]{8,20}$";
        return null;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ListStructuralResidualsAfterRender(
        string structuredContent,
        IReadOnlyDictionary<string, string> values) =>
        AiDirectFilledDocumentValidator.FindRemainingPlaceholders(RenderContent(structuredContent, values));

    public VariableValidationResult ValidateValues(
        IReadOnlyList<DetectedTemplateVariable> variables,
        IReadOnlyDictionary<string, string> values)
    {
        var missing = new List<string>();
        var invalid = new List<string>();

        foreach (var v in variables)
        {
            var has = TryGetValueIgnoreCase(values, v.Name, out var raw);
            var val = raw ?? "";
            if (v.IsRequired && string.IsNullOrWhiteSpace(val))
            {
                missing.Add(v.Name);
                continue;
            }

            if (string.IsNullOrWhiteSpace(val))
                continue;

            if (IRibValidationService.IsRibLikeVariableName(v.Name))
            {
                if (!ribValidation.IsValidRibDigits(val))
                    invalid.Add(v.Name);
                continue;
            }

            if (string.IsNullOrWhiteSpace(v.ValidationRule))
                continue;

            try
            {
                if (!Regex.IsMatch(val.Trim(), v.ValidationRule, RegexOptions.Compiled))
                    invalid.Add(v.Name);
            }
            catch (ArgumentException)
            {
                invalid.Add(v.Name);
            }
        }

        var ok = missing.Count == 0 && invalid.Count == 0;
        return new VariableValidationResult(ok, missing, invalid);
    }

    public VariableValidationResult ValidateVariables(
        IReadOnlyList<DetectedTemplateVariable> variables,
        IReadOnlyDictionary<string, string> values) =>
        ValidateValues(variables, values);

    private static bool TryGetValueIgnoreCase(
        IReadOnlyDictionary<string, string> values,
        string key,
        out string? value)
    {
        foreach (var kv in values)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kv.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    public string BuildRuleBasedContent(string description, IReadOnlyList<string> variableNames)
    {
        var variables = variableNames.Count == 0
            ? "{{nom}}, {{prenom}}, {{cin}}, {{poste}}, {{date_embauche}}, {{departement}}"
            : string.Join(", ", variableNames.Select(v => $"{{{{{v}}}}}"));

        return
            $"[EN_TETE]\n" +
            "Société: MyKyntus Maroc\n" +
            "Ville: Casablanca\n" +
            "Date: {{date}}\n\n" +
            "[CORPS]\n" +
            $"Objet: {description}\n" +
            $"Informations collaborateur: {variables}\n" +
            "Ce document est établi pour servir et valoir ce que de droit.\n\n" +
            "[SIGNATURE]\n" +
            "Direction des Ressources Humaines";
    }

    public string RenderContent(string structuredContent, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(structuredContent))
            return structuredContent;

        var normalizedContent = DocxPlainTextExtractor.NormalizeForPlaceholders(structuredContent);
        var baseText = TryGetUploadBodyText(normalizedContent) ?? normalizedContent;
        var rendered = baseText;

        foreach (var kvp in values)
        {
            var outVal = kvp.Value ?? string.Empty;
            var type = InferVariableType(kvp.Key.ToLowerInvariant());
            if (type == "date" && TryFormatIsoDateToFrench(outVal, out var fr))
                outVal = fr;
            rendered = rendered.Replace($"{{{{{kvp.Key}}}}}", outVal, StringComparison.OrdinalIgnoreCase);
        }

        rendered = HumanPlaceholderRegex.Replace(rendered, match =>
        {
            var token = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(token))
                return match.Value;
            var normalized = placeholderNormalization.NormalizeKey(token);
            var canonical = placeholderNormalization.ResolveCanonicalKey(normalized);
            if (TryGetValueIgnoreCase(values, canonical, out var resolved) && !string.IsNullOrWhiteSpace(resolved))
                return resolved!;
            if (TryGetValueIgnoreCase(values, normalized, out resolved) && !string.IsNullOrWhiteSpace(resolved))
                return resolved!;
            return match.Value;
        });

        rendered = ApplyNamedLiteralPlaceholdersWhenFilled(rendered, values);
        var withHonorific = NormalizeHonorificText(rendered, values);
        var withBankAndCin = NormalizeCinAndRibText(withHonorific, values);
        return NormalizeProfessionalFormatting(withBankAndCin);
    }

    /// <summary>
    /// Remplace uniquement les marqueurs nommés classiques lorsqu’une valeur existe (pas de substitution silencieuse vide).
    /// Les <c>(X)</c> multiples partagent la clé <c>marqueur_x</c> / <c>placeholder_generique</c> si renseignée.
    /// </summary>
    private static string ApplyNamedLiteralPlaceholdersWhenFilled(string text, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var nom = FirstNonEmpty(values, "nom_complet", "nom", "prenom_nom");
        if (!string.IsNullOrWhiteSpace(nom))
            text = text.Replace("(NOM)", nom.Trim(), StringComparison.OrdinalIgnoreCase);

        var prenom = FirstNonEmpty(values, "prenom");
        if (!string.IsNullOrWhiteSpace(prenom))
            text = text.Replace("(PRENOM)", prenom.Trim(), StringComparison.OrdinalIgnoreCase);

        var poste = FirstNonEmpty(values, "poste", "fonction", "qualite", "implique", "role");
        if (!string.IsNullOrWhiteSpace(poste))
            text = text.Replace("(POSTE)", poste.Trim(), StringComparison.OrdinalIgnoreCase);

        var dateFr = ResolveFrenchDate(values);
        if (!string.IsNullOrWhiteSpace(dateFr))
        {
            text = text.Replace("(DATE)", dateFr, StringComparison.OrdinalIgnoreCase);
            text = LegacyDateMaskRegex.Replace(text, dateFr);
        }

        var bank = FirstNonEmpty(values, "compte_bancaire", "rib", "iban", "numero_compte");
        if (!string.IsNullOrWhiteSpace(bank))
            text = text.Replace("(XXXX)", bank.Trim(), StringComparison.Ordinal);

        var marqueur = FirstNonEmpty(values, "marqueur_x", "placeholder_x", "placeholder_generique");
        if (!string.IsNullOrWhiteSpace(marqueur))
        {
            var m = marqueur.Trim();
            text = text.Replace("(X)", m, StringComparison.OrdinalIgnoreCase);
        }

        return text;
    }

    /// <summary>Si la valeur est une date ISO <c>yyyy-MM-dd</c>, retourne l’équivalent <c>dd/MM/yyyy</c> (fr-FR).</summary>
    private static bool TryFormatIsoDateToFrench(string value, out string formatted)
    {
        formatted = value;
        var t = value.Trim();
        if (!IsoDateYyyyMmDdRegex.IsMatch(t))
            return false;
        if (!DateTime.TryParseExact(t, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return false;
        formatted = d.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("fr-FR"));
        return true;
    }

    private static string? FirstNonEmpty(IReadOnlyDictionary<string, string> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
                return v;
        }

        return null;
    }

    private static string? ResolveFrenchDate(IReadOnlyDictionary<string, string> values)
    {
        var raw = FirstNonEmpty(values, "date_fr", "date_effet", "date_document", "date");
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var t = raw.Trim();
        if (IsoDateYyyyMmDdRegex.IsMatch(t)
            && DateTime.TryParseExact(t, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
            return iso.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("fr-FR"));
        if (DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            || DateTime.TryParse(t, CultureInfo.GetCultureInfo("fr-FR"), DateTimeStyles.None, out d))
            return d.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("fr-FR"));
        return raw;
    }

    /// <summary>Nettoie les formulations figées type « Monsieur/Madame » quand une civilité explicite est disponible.</summary>
    private static string NormalizeHonorificText(string text, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var civilite = FirstNonEmpty(values, "civilite");
        var civ = NormalizeCivilite(civilite);
        if (string.IsNullOrWhiteSpace(civ))
            return text;

        var output = text.Replace("Monsieur/Madame", civ, StringComparison.OrdinalIgnoreCase)
            .Replace("M./Mme", civ, StringComparison.OrdinalIgnoreCase)
            .Replace("M/Mme", civ, StringComparison.OrdinalIgnoreCase);

        return output;
    }

    private static string? NormalizeCivilite(string? civilite)
    {
        if (string.IsNullOrWhiteSpace(civilite))
            return null;
        var c = civilite.Trim();
        if (c.StartsWith("mad", StringComparison.OrdinalIgnoreCase))
            return "Madame";
        if (c.StartsWith("m", StringComparison.OrdinalIgnoreCase))
            return "Monsieur";
        return null;
    }

    private string NormalizeCinAndRibText(string text, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var output = text;
        var cin = FirstNonEmpty(values, "numero_cin", "cin", "cin_nr", "nr_cin");
        if (IsUsableCin(cin))
        {
            output = CinPlaceholderRegex.Replace(
                output,
                m => m.Groups[1].Value + cin!.Trim());
        }

        var rib = FirstNonEmpty(values, "rib", "compte_bancaire", "numero_compte", "iban");
        if (IsUsableRib(rib))
        {
            output = RibPlaceholderRegex.Replace(
                output,
                m => m.Groups[1].Value + rib!.Trim());
        }

        return output;
    }

    private static bool IsUsableCin(string? cin)
    {
        if (string.IsNullOrWhiteSpace(cin))
            return false;
        var t = cin.Trim();
        if (t is "-" or "—")
            return false;
        if (ContainsAny(t.ToLowerInvariant(), "pilote", "coach", "manager", "admin", "test", "demo", "xxx", "n/a"))
            return false;
        return CinValueRegex.IsMatch(t);
    }

    private bool IsUsableRib(string? rib)
    {
        if (string.IsNullOrWhiteSpace(rib))
            return false;
        var t = rib.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        if (t is "-" or "—")
            return false;
        if (ContainsAny(t.ToLowerInvariant(), "test", "demo", "xxx", "n/a"))
            return false;
        return ribValidation.IsValidRibDigits(t);
    }

    /// <summary>Nettoyage final : titre dupliqué, espaces typographiques et paragraphes.</summary>
    private static string NormalizeProfessionalFormatting(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n').ToList();
        RemoveDuplicatedHeadingPrefix(lines);

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                lines[i] = string.Empty;
                continue;
            }

            line = MultiSpaceRegex.Replace(line.Trim(), " ");
            line = MissingSentenceSpaceRegex.Replace(line, "$1$2 $3");
            line = line.Replace(" ,", ",", StringComparison.Ordinal)
                .Replace(" .", ".", StringComparison.Ordinal)
                .Replace(" ;", ";", StringComparison.Ordinal)
                .Replace(" !", "!", StringComparison.Ordinal)
                .Replace(" ?", "?", StringComparison.Ordinal)
                .Replace(" :", " :", StringComparison.Ordinal);
            lines[i] = line;
        }

        var compact = new List<string>(lines.Count);
        var previousBlank = false;
        foreach (var ln in lines)
        {
            var isBlank = string.IsNullOrWhiteSpace(ln);
            if (isBlank)
            {
                if (previousBlank)
                    continue;
                compact.Add(string.Empty);
                previousBlank = true;
                continue;
            }

            compact.Add(ln);
            previousBlank = false;
        }

        return string.Join("\n", compact).Trim();
    }

    /// <summary>Supprime la répétition du titre quand la ligne suivante recommence exactement par ce titre.</summary>
    private static void RemoveDuplicatedHeadingPrefix(List<string> lines)
    {
        if (lines.Count < 2)
            return;

        var headingIndex = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;
            headingIndex = i;
            break;
        }

        if (headingIndex < 0)
            return;

        var heading = lines[headingIndex].Trim();
        if (heading.Length < 8)
            return;

        var nextIndex = -1;
        for (var i = headingIndex + 1; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;
            nextIndex = i;
            break;
        }

        if (nextIndex < 0)
            return;

        var normalizedHeading = MultiSpaceRegex.Replace(heading, " ");
        var nextLine = lines[nextIndex].TrimStart();
        if (!nextLine.StartsWith(normalizedHeading, StringComparison.OrdinalIgnoreCase))
            return;

        var stripped = nextLine[normalizedHeading.Length..].TrimStart(' ', '-', ':', ',', ';');
        lines[nextIndex] = stripped;
    }

    /// <summary>Contenu uploadé DOCX/texte : JSON avec <c>bodyText</c> — on substitue les placeholders sur ce texte, pas sur tout le JSON.</summary>
    private static string? TryGetUploadBodyText(string structuredContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(structuredContent);
            if (doc.RootElement.TryGetProperty("bodyText", out var bt) && bt.ValueKind == JsonValueKind.String)
            {
                var s = bt.GetString();
                return string.IsNullOrEmpty(s) ? null : s;
            }
        }
        catch (JsonException)
        {
            /* contenu non-JSON (règles / texte brut) */
        }

        return null;
    }
}
