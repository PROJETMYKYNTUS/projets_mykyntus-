using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DocumentationBackend.Application.Abstractions;
using DocumentationBackend.Configuration;
using Microsoft.Extensions.Options;

namespace DocumentationBackend.Infrastructure.Ai;

/// <summary>Client HTTP OpenAI-compatible (chat completions).</summary>
public sealed class OpenAiCompatibleTemplateContentGenerator : IAiTemplateContentGenerator
{
    private const string DetectVariablesSystemPrompt =
        "You are an expert HR document analyst. Given a document template body text, " +
        "identify every variable placeholder — including {{var}} patterns, (X) markers, " +
        "(XXXX) markers, and XX/XX/YYYY date masks. " +
        "Return a JSON object with a single key \"variables\" whose value is an array of objects, each with: " +
        "{ \"name\": string, \"type\": \"text\"|\"date\"|\"number\"|\"email\"|\"phone\"|\"boolean\", " +
        "\"isRequired\": boolean, \"validationRule\": string|null, \"description\": string }. " +
        "Rules:\n" +
        "- name must be snake_case, lowercase, no spaces.\n" +
        "- For (X) markers use contextual names inferred from surrounding text (e.g. \"nom_complet\", \"cin\", \"poste\").\n" +
        "- For XX/XX/YYYY use name \"date_document\".\n" +
        "- For (XXXX) use name \"compte_bancaire\".\n" +
        "- type inference: contains date/naissance/embauche → date; salaire/montant/brut/net/prime/indemnite/cotisation/taux → number; email/courriel → email; tel/phone/portable → phone; actif/valide/consent → boolean; else text.\n" +
        "- isRequired: false only if the placeholder is clearly optional in context.\n" +
        "- validationRule: regex string for cin, email, phone, iban/rib — null otherwise.\n" +
        "- description: one short sentence in French explaining what to fill.\n" +
        "Return ONLY the JSON object. No markdown, no preamble.";

    private const string SuggestMetaSystemPrompt =
        "You are an HR document specialist. Given a document template text, suggest:\n" +
        "- a short template code (uppercase, hyphens, max 20 chars, e.g. ATT-TRAVAIL-001)\n" +
        "- a display name in French (max 60 chars)\n" +
        "- a one-sentence description in French (max 160 chars)\n" +
        "Return ONLY a JSON object: { \"suggestedCode\": string, \"suggestedName\": string, \"suggestedDescription\": string }\n" +
        "No markdown, no preamble.";

    private const string RhDirectFillSystemPrompt =
        "Tu es un assistant expert en rédaction de documents RH.\n\n" +
        "Ta mission est de remplir un template en utilisant les données fournies.\n\n" +
        "Règles :\n" +
        "- Remplacer tous les placeholders comme (X), (), _, -, ou similaires\n" +
        "- Utiliser uniquement les données fournies\n" +
        "- Ne rien inventer\n" +
        "- Respecter un ton professionnel\n" +
        "- Garder la structure du document\n\n" +
        "Si une donnée est manquante :\n" +
        "→ répondre exactement : ERROR_MISSING_DATA\n\n" +
        "Répondre uniquement avec le document final.";

    private const string RefineVariablesSystemPrompt =
        "You correct French HR template variable values for Morocco-facing documents.\n" +
        "You receive JSON with: documentTitle (string), templateVariableNames (array of allowed keys), values (object of current key→value).\n" +
        "Return ONLY: { \"updates\": { \"key\": \"value\", ... } } with ONLY keys you change. Keys must be snake_case or match input keys exactly.\n" +
        "Rules:\n" +
        "- civilite: output exactly \"Monsieur\" or \"Madame\" using French first-name conventions; never \"Monsieur/Madame\" or both.\n" +
        "- cin: plausible Moroccan national ID (letters+digits) or empty string \"\" if current value is a placeholder or wrong (pilote, coach, manager, test, demo, xxx, —, -, n/a, admin, rh, etc.).\n" +
        "- poste: real job title in French (e.g. \"Chargé de clientèle\", \"Technico-commercial\"). Never use application role names as job title: pilote, coach, manager, rp, rh, admin, audit (case-insensitive). If occupation unknown, use \"Collaborateur\".\n" +
        "- departement, service, cellule, pole: organizational unit or site; must not duplicate the job title alone.\n" +
        "- fonction, qualite: professional capacity aligned with poste; not the raw app role string.\n" +
        "- nom_complet: \"Prénom Nom\" without repeating civilite if civilite is a separate variable.\n" +
        "- If values contain \"Monsieur/Madame\" or \"Madame/Monsieur\", normalize to a single consistent civility via civilite and keep nom_complet without civility.\n" +
        "- rib / compte_bancaire / numero_compte: keep only plausible bank-format values; if unknown placeholder (\"—\", \"xxx\", \"test\"), set empty string.\n" +
        "- Do not invent a real CIN number; use \"\" if unknown.\n" +
        "- Never include an update for the variable key \"role\" (application role: pilote, coach, etc.); use \"poste\" for job title.\n" +
        "If nothing should change, return {\"updates\":{}}.";

    private static readonly Regex SafeVariableName = new(@"^[a-z][a-z0-9_]{1,63}$", RegexOptions.Compiled);

    private readonly HttpClient _http;
    private readonly AiTemplateOptions _options;
    private readonly IAiApiKeyResolver _apiKeyResolver;
    private readonly ILogger<OpenAiCompatibleTemplateContentGenerator> _logger;

    public OpenAiCompatibleTemplateContentGenerator(
        HttpClient http,
        IOptions<AiTemplateOptions> aiOptions,
        IAiApiKeyResolver apiKeyResolver,
        ILogger<OpenAiCompatibleTemplateContentGenerator> logger)
    {
        _http = http;
        _options = aiOptions.Value;
        _apiKeyResolver = apiKeyResolver;
        _logger = logger;
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        if (!string.IsNullOrEmpty(baseUrl))
            _http.BaseAddress = new Uri(baseUrl + "/");
    }

    /// <inheritdoc />
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return (await ResolveApiKeysAsync(cancellationToken).ConfigureAwait(false)).Count > 0;
    }

    private async Task<string?> ResolveApiKeyAsync(CancellationToken cancellationToken)
    {
        return (await ResolveApiKeysAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault();
    }

    private async Task<IReadOnlyList<string>> ResolveApiKeysAsync(CancellationToken cancellationToken)
    {
        var keys = new List<string>();
        var dbKeys = await _apiKeyResolver.GetFallbackApiKeysAsync(cancellationToken).ConfigureAwait(false);
        foreach (var key in dbKeys)
        {
            if (!string.IsNullOrWhiteSpace(key))
                keys.Add(key.Trim());
        }

        var opt = _options.ApiKey?.Trim();
        if (!string.IsNullOrWhiteSpace(opt))
            keys.Add(opt);

        return keys
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private async Task<HttpResponseMessage> SendWithApiKeyFallbackAsync(
        object body,
        string operationName,
        CancellationToken cancellationToken)
    {
        var apiKeys = await ResolveApiKeysAsync(cancellationToken).ConfigureAwait(false);
        if (apiKeys.Count == 0)
            throw new InvalidOperationException("Clé API IA non configurée (admin ai_api_keys ou DocumentTemplates:Ai:ApiKey).");

        HttpStatusCode? lastStatus = null;
        string? lastBody = null;

        for (var i = 0; i < apiKeys.Count; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKeys[i]);
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            var res = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (res.IsSuccessStatusCode)
                return res;

            var raw = await res.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            lastStatus = res.StatusCode;
            lastBody = raw;

            if (!ShouldRetryWithAnotherKey(res.StatusCode, raw) || i == apiKeys.Count - 1)
            {
                _logger.LogWarning("IA {Operation} HTTP {Status}: {Body}", operationName, (int)res.StatusCode, raw);
                throw new InvalidOperationException($"API IA indisponible ({(int)res.StatusCode}).");
            }

            _logger.LogWarning(
                "IA {Operation} HTTP {Status}; tentative avec une autre clé API ({Attempt}/{Total})",
                operationName,
                (int)res.StatusCode,
                i + 2,
                apiKeys.Count);

            res.Dispose();
        }

        throw new InvalidOperationException(
            lastStatus.HasValue
                ? $"API IA indisponible ({(int)lastStatus.Value})."
                : "API IA indisponible.");
    }

    public async Task<string> GenerateStructuredTemplateJsonAsync(string description, CancellationToken cancellationToken = default)
    {
        if (!await IsAvailableAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Clé API IA non configurée (admin ou DocumentTemplates:Ai:ApiKey).");

        var prompt =
            "Generate a professional document template as a single JSON object based on this description:\n" +
            description.Trim() +
            "\n\nRequirements:\n" +
            "- Follow international business document standards and, where relevant, common national HR/legal conventions (Morocco-friendly when applicable).\n" +
            "- Include structured sections: use keys \"title\" (string), \"sections\" (array of { \"heading\": string, \"subheading\": string | null, \"body\": string }).\n" +
            "- Add \"variables\" as array of suggested placeholder names (snake_case) used in bodies as {{variable}}.\n" +
            "- Output ONLY valid JSON, no markdown fences.\n";

        var body = new
        {
            model = _options.Model,
            temperature = 0.35,
            max_tokens = _options.MaxTokens,
            messages = new object[]
            {
                new { role = "system", content = "You output only compact JSON for document templates." },
                new { role = "user", content = prompt },
            },
        };

        using var res = await SendWithApiKeyFallbackAsync(body, "template generation", cancellationToken).ConfigureAwait(false);
        var raw = await res.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var trimmed = ExtractMessageContentFromChatResponse(raw);
        using var _ = JsonDocument.Parse(trimmed);
        return trimmed;
    }

    public async Task<IReadOnlyList<string>> InferVariableNamesFromUploadTextAsync(
        string documentPlainText,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAvailableAsync(cancellationToken).ConfigureAwait(false))
            return Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(documentPlainText))
            return Array.Empty<string>();
        if (!_options.InferVariablesOnUpload)
            return Array.Empty<string>();

        var maxChars = Math.Clamp(_options.MaxCharsForUploadInference, 2000, 48000);
        var truncated = documentPlainText.Length > maxChars
            ? documentPlainText[..maxChars] + "\n\n[… texte tronqué pour l’analyse …]"
            : documentPlainText;

        var prompt =
            "You analyze HR / administrative document text. Identify data that typically changes per person or case " +
            "(employee name, ID number, address, dates, salary amounts, job title, department, signature dates, etc.). " +
            "Ignore fixed legal boilerplate that never changes.\n\n" +
            "Return a single JSON object with key \"variables\": array of strings in snake_case (ASCII letters, digits, underscore; " +
            "start with a letter). Maximum 40 items. No duplicates. If the text already contains {{like_this}} placeholders, " +
            "include matching names.\n\n" +
            "Document text:\n---\n" +
            truncated;

        var body = new
        {
            model = _options.Model,
            temperature = 0.15,
            max_tokens = 1200,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "You reply with only valid JSON. The object must contain a \"variables\" array of strings.",
                },
                new { role = "user", content = prompt },
            },
        };

        try
        {
            using var res = await SendWithApiKeyFallbackAsync(body, "upload variable inference", cancellationToken).ConfigureAwait(false);
            var raw = await res.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            var content = ExtractMessageContentFromChatResponse(raw);
            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("variables", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return Array.Empty<string>();

            var list = new List<string>();
            foreach (var el in arr.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.String)
                    continue;
                var name = el.GetString()?.Trim();
                if (string.IsNullOrEmpty(name))
                    continue;
                name = name.Replace(" ", "_", StringComparison.Ordinal);
                if (SafeVariableName.IsMatch(name))
                    list.Add(name);
            }

            return list.Distinct(StringComparer.OrdinalIgnoreCase).Take(40).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Parse JSON inférence variables upload échoué");
            return Array.Empty<string>();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AiDetectedVariable>> DetectVariablesFromTextAsync(
        string bodyText,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAvailableAsync(cancellationToken).ConfigureAwait(false) || !_options.EnableVariableDetection || string.IsNullOrWhiteSpace(bodyText))
            return Array.Empty<AiDetectedVariable>();

        var maxChars = Math.Clamp(_options.MaxBodyCharsForDetection, 500, 32000);
        var truncated = bodyText.Length > maxChars ? bodyText[..maxChars] : bodyText;

        var body = new
        {
            model = _options.Model,
            temperature = 0.0,
            max_tokens = 1500,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = DetectVariablesSystemPrompt },
                new { role = "user", content = truncated },
            },
        };

        try
        {
            using var res = await SendWithApiKeyFallbackAsync(body, "detect variables", cancellationToken).ConfigureAwait(false);
            var raw = await res.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            var content = ExtractMessageContentFromChatResponse(raw);
            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("variables", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return Array.Empty<AiDetectedVariable>();

            var list = new List<AiDetectedVariable>();
            foreach (var el in arr.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object)
                    continue;
                var name = el.TryGetProperty("name", out var nEl) && nEl.ValueKind == JsonValueKind.String
                    ? nEl.GetString()?.Trim().Replace(" ", "_", StringComparison.Ordinal)
                    : null;
                if (string.IsNullOrEmpty(name) || !SafeVariableName.IsMatch(name))
                    continue;
                var type = NormalizeAiType(el.TryGetProperty("type", out var tEl) ? tEl.GetString() : null);
                var isReq = el.TryGetProperty("isRequired", out var rEl) && rEl.ValueKind == JsonValueKind.True;
                string? rule = null;
                if (el.TryGetProperty("validationRule", out var vrEl) && vrEl.ValueKind == JsonValueKind.String)
                {
                    var rs = vrEl.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(rs))
                        rule = rs;
                }

                string? desc = null;
                if (el.TryGetProperty("description", out var dEl) && dEl.ValueKind == JsonValueKind.String)
                    desc = dEl.GetString()?.Trim();

                list.Add(new AiDetectedVariable(name, type, isReq, rule, desc));
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "IA DetectVariablesFromText parse or network failed");
            return Array.Empty<AiDetectedVariable>();
        }
    }

    /// <inheritdoc />
    public async Task<AiTemplateMetaSuggestion> SuggestTemplateMetaAsync(
        string bodyText,
        string? userHint = null,
        CancellationToken cancellationToken = default)
    {
        var fallback = new AiTemplateMetaSuggestion("TPL-UPLOAD", "Template importé", "Template importé automatiquement.");
        if (!await IsAvailableAsync(cancellationToken).ConfigureAwait(false) || !_options.EnableMetaSuggestion)
            return fallback;
        if (string.IsNullOrWhiteSpace(bodyText))
            return fallback;

        var maxChars = Math.Clamp(_options.MaxBodyCharsForMeta, 200, 16000);
        var truncated = bodyText.Length > maxChars ? bodyText[..maxChars] : bodyText;
        var userContent = string.IsNullOrWhiteSpace(userHint)
            ? truncated
            : truncated + "\nHint: " + userHint.Trim();

        var body = new
        {
            model = _options.Model,
            temperature = 0.0,
            max_tokens = 200,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = SuggestMetaSystemPrompt },
                new { role = "user", content = userContent },
            },
        };

        try
        {
            using var res = await SendWithApiKeyFallbackAsync(body, "suggest template meta", cancellationToken).ConfigureAwait(false);
            var raw = await res.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            var content = ExtractMessageContentFromChatResponse(raw);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var code = root.TryGetProperty("suggestedCode", out var cEl) && cEl.ValueKind == JsonValueKind.String
                ? cEl.GetString()?.Trim() ?? "TPL-UPLOAD"
                : "TPL-UPLOAD";
            var nm = root.TryGetProperty("suggestedName", out var nEl) && nEl.ValueKind == JsonValueKind.String
                ? nEl.GetString()?.Trim() ?? "Template importé"
                : "Template importé";
            var desc = root.TryGetProperty("suggestedDescription", out var dEl) && dEl.ValueKind == JsonValueKind.String
                ? dEl.GetString()?.Trim() ?? fallback.SuggestedDescription
                : fallback.SuggestedDescription;
            return new AiTemplateMetaSuggestion(code, nm, desc);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "IA SuggestTemplateMeta parse or network failed");
            return fallback;
        }
    }

    /// <inheritdoc />
    /// <inheritdoc />
    public async Task<AiDirectRawFillResult> FillRhTemplateDirectAsync(
        string template,
        JsonElement dbData,
        JsonElement formData,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAvailableAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Clé API IA non configurée (admin ou DocumentTemplates:Ai:ApiKey).");
        if (string.IsNullOrWhiteSpace(template))
            throw new ArgumentException("Le template est obligatoire.", nameof(template));

        var userJson = JsonSerializer.Serialize(
            new { template, dbData, formData },
            AiPayloadJsonOptions);

        var maxChars = Math.Clamp(_options.MaxCharsVariableRefinementPayload * 4, 4000, 120_000);
        if (userJson.Length > maxChars)
            throw new InvalidOperationException($"Payload IA direct trop volumineux (max {maxChars} caractères).");

        var body = new
        {
            model = _options.Model,
            temperature = 0.2,
            max_tokens = Math.Clamp(_options.DirectFillMaxTokens, 512, 128_000),
            messages = new object[]
            {
                new { role = "system", content = RhDirectFillSystemPrompt },
                new { role = "user", content = userJson },
            },
        };

        using var res = await SendWithApiKeyFallbackAsync(body, "direct fill", cancellationToken).ConfigureAwait(false);
        var rawHttp = await res.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var content = ExtractMessageContentFromChatResponse(rawHttp).Trim();
        if (IsErrorMissingDataToken(content))
            return new AiDirectRawFillResult(string.Empty, ModelReportedMissingData: true);

        return new AiDirectRawFillResult(content, ModelReportedMissingData: false);
    }

    private static bool IsErrorMissingDataToken(string content)
    {
        if (string.IsNullOrEmpty(content))
            return false;
        var t = content.Trim().Trim('"');
        return string.Equals(t, "ERROR_MISSING_DATA", StringComparison.Ordinal);
    }

    public async Task<IReadOnlyDictionary<string, string>> RefineMergedVariablesForDocumentAsync(
        IReadOnlyDictionary<string, string> mergedVariables,
        IReadOnlyList<string> templateVariableNames,
        string? documentTitle,
        CancellationToken cancellationToken = default)
    {
        var empty = (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!await IsAvailableAsync(cancellationToken).ConfigureAwait(false) || !_options.EnableVariableRefinementOnGenerate)
            return empty;
        if (mergedVariables.Count == 0)
            return empty;

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in templateVariableNames)
        {
            if (!string.IsNullOrWhiteSpace(n))
                allowed.Add(n.Trim());
        }

        foreach (var k in mergedVariables.Keys)
            allowed.Add(k);

        var valuesForPrompt = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        const int maxValLen = 280;
        foreach (var kv in mergedVariables)
        {
            var v = kv.Value ?? "";
            if (v.Length > maxValLen)
                v = v[..maxValLen] + "…";
            valuesForPrompt[kv.Key] = v;
        }

        var namesForPrompt = templateVariableNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var userObj = new
        {
            documentTitle = documentTitle ?? "",
            templateVariableNames = namesForPrompt,
            values = valuesForPrompt,
        };
        var userJson = JsonSerializer.Serialize(userObj, AiPayloadJsonOptions);
        var maxPayload = Math.Clamp(_options.MaxCharsVariableRefinementPayload, 800, 32000);
        if (userJson.Length > maxPayload)
            userJson = userJson[..maxPayload] + "\n[truncated]";

        var body = new
        {
            model = _options.Model,
            temperature = 0.0,
            max_tokens = 700,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = RefineVariablesSystemPrompt },
                new { role = "user", content = userJson },
            },
        };

        try
        {
            using var res = await SendWithApiKeyFallbackAsync(body, "refine variables", cancellationToken).ConfigureAwait(false);
            var raw = await res.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            var content = ExtractMessageContentFromChatResponse(raw);
            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("updates", out var upd) || upd.ValueKind != JsonValueKind.Object)
                return empty;

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in upd.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.String)
                    continue;
                var key = prop.Name.Trim();
                if (string.IsNullOrEmpty(key) || !allowed.Contains(key))
                    continue;
                var val = prop.Value.GetString() ?? "";
                if (val.Length > 500)
                    val = val[..500];
                result[key] = val.Trim();
            }

            if (result.Count > 0)
            {
                _logger.LogInformation(
                    "IA refined {RefinedKeyCount} template variable keys for document title {DocumentTitle}",
                    result.Count,
                    string.IsNullOrWhiteSpace(documentTitle) ? "(none)" : documentTitle.Trim());
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "IA RefineMergedVariables parse or network failed");
            return empty;
        }
    }

    private static readonly JsonSerializerOptions AiPayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static string NormalizeAiType(string? t)
    {
        var s = (t ?? "text").Trim().ToLowerInvariant();
        return s switch
        {
            "date" or "number" or "email" or "phone" or "boolean" or "text" => s,
            _ => "text",
        };
    }

    private static string ExtractMessageContentFromChatResponse(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Réponse IA vide.");

        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var end = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (end > 3)
                trimmed = trimmed[3..end].Trim();
            if (trimmed.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed[4..].Trim();
        }

        return trimmed;
    }

    private static bool ShouldRetryWithAnotherKey(HttpStatusCode statusCode, string rawBody)
    {
        if (statusCode == HttpStatusCode.TooManyRequests)
            return true;

        if (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.Forbidden)
            return true;

        if ((int)statusCode >= 500)
            return true;

        return rawBody.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase);
    }
}
