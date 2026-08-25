using System.Globalization;
using System.Text.Json;
using Prime.Application.DTOs;

namespace Prime.Application;

/// <summary>
/// Extrait des défauts de pondération Prime/Challenge depuis le schemaJson d’un template / draft.
/// Utilise le 1er secteur horizontal qui a une valeur numérique.
/// </summary>
public static class TemplateSchemaPonderationHints
{
    public static IReadOnlyList<TemplateCommonLineHint> FromSchemaJson(string? schemaJson)
    {
        var result = new List<TemplateCommonLineHint>();
        var raw = (schemaJson ?? "").Trim();
        if (raw.Length == 0) return result;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("lines", out var lines) ||
                lines.ValueKind != JsonValueKind.Array)
                return result;

            var idx = 0;
            foreach (var line in lines.EnumerateArray())
            {
                var stable = GetString(line, "stableId");
                if (stable.Length == 0) continue;

                var contract = GetString(line, "contract").ToUpperInvariant();
                // Lignes partie commune uniquement (RACC / SAV).
                if (contract is not ("RACC" or "SAV" or "RACC/SAV" or "RACC-SAV"))
                {
                    // Certains schémas omettent le filtre strict — on accepte tout stableId.
                }

                var label = GetString(line, "indicator");
                decimal? prime = null;
                decimal? challenge = null;

                if (line.TryGetProperty("secteurs", out var secteurs) &&
                    secteurs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var secteur in secteurs.EnumerateArray())
                    {
                        if (!secteur.TryGetProperty("defaults", out var defaults) ||
                            defaults.ValueKind != JsonValueKind.Object)
                            continue;

                        prime ??= ParsePct(GetString(defaults, "ponderationPrime"));
                        challenge ??= ParsePct(GetString(defaults, "ponderationChallenge"));
                        if (prime is not null || challenge is not null) break;
                    }
                }

                result.Add(new TemplateCommonLineHint
                {
                    TemplateStableId = stable,
                    Label = label.Length > 0 ? label : stable,
                    Contract = contract,
                    SortOrder = idx++,
                    TemplatePrimePct = prime,
                    TemplateChallengePct = challenge,
                });
            }
        }
        catch
        {
            return [];
        }

        return result;
    }

    private static string GetString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p)) return "";
        return p.ValueKind switch
        {
            JsonValueKind.String => (p.GetString() ?? "").Trim(),
            JsonValueKind.Number => p.GetRawText().Trim(),
            _ => "",
        };
    }

    private static decimal? ParsePct(string raw)
    {
        var t = (raw ?? "").Trim().Replace(',', '.');
        if (t.Length == 0 || t == "—") return null;
        if (t.EndsWith('%')) t = t[..^1].Trim();
        if (!decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var n))
            return null;
        if (n < 0 || n > 100) return null;
        return Math.Round(n, 4, MidpointRounding.AwayFromZero);
    }
}
