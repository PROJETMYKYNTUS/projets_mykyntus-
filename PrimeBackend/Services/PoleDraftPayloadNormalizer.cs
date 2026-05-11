using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace PrimeBackend.Services;

/// <summary>
/// Pour le contrat SAV, la répartition RDV n’est pas un indicateur métier : on normalise le JSON
/// (suppression / valeur numérique) pour éviter les résidus Excel (tirets, pourcentages mal typés, etc.).
/// </summary>
public static class PoleDraftPayloadNormalizer
{
    private static readonly Regex PercentRegex = new(
        @"^\s*(-?\d+(?:[.,]\d+)?)\s*%\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string NormalizePoleSaisieJson(string? schemaJson, string poleSaisieJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson) || string.IsNullOrWhiteSpace(poleSaisieJson))
            return poleSaisieJson;

        try
        {
            var schemaRoot = JsonNode.Parse(schemaJson);
            var lines = schemaRoot?["lines"] as JsonArray;
            if (lines is null || lines.Count == 0)
                return poleSaisieJson;

            var savStableIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in lines)
            {
                if (line is not JsonObject lo) continue;
                var contract = lo["contract"]?.GetValue<string>()?.Trim() ?? "";
                if (!contract.Equals("SAV", StringComparison.OrdinalIgnoreCase)) continue;
                var sid = lo["stableId"]?.GetValue<string>()?.Trim();
                if (!string.IsNullOrEmpty(sid)) savStableIds.Add(sid);
            }

            if (savStableIds.Count == 0)
                return poleSaisieJson;

            var poleRoot = JsonNode.Parse(poleSaisieJson) as JsonObject
                           ?? JsonNode.Parse("{}")!.AsObject();
            var lignes = poleRoot["lignes"] as JsonObject;
            if (lignes is null)
                return poleSaisieJson;

            foreach (var stableId in savStableIds)
            {
                if (!lignes.TryGetPropertyValue(stableId, out var lineNode) || lineNode is not JsonObject lineObj)
                    continue;
                NormalizeSavRepartitionLine(lineObj);
            }

            return poleRoot.ToJsonString();
        }
        catch
        {
            return poleSaisieJson;
        }
    }

    private static void NormalizeSavRepartitionLine(JsonObject lineObj)
    {
        if (!lineObj.TryGetPropertyValue("repartitionRdv", out var repNode))
            return;

        if (repNode is null)
        {
            lineObj.Remove("repartitionRdv");
            return;
        }

        if (repNode is JsonValue jvNull && jvNull.GetValueKind() == JsonValueKind.Null)
        {
            lineObj.Remove("repartitionRdv");
            return;
        }

        if (TryParseRepartitionNode(repNode, out var num))
        {
            lineObj["repartitionRdv"] = num;
            return;
        }

        lineObj.Remove("repartitionRdv");
    }

    private static bool TryParseRepartitionNode(JsonNode? node, out double value)
    {
        value = 0;
        if (node is JsonValue jv)
        {
            if (jv.TryGetValue(out double d))
            {
                value = d;
                return true;
            }

            if (jv.TryGetValue(out int i))
            {
                value = i;
                return true;
            }

            if (jv.TryGetValue(out long l))
            {
                value = l;
                return true;
            }

            var s = jv.TryGetValue(out string? str) ? str : jv.ToString();
            return TryParseRepartitionString(s ?? "", out value);
        }

        return false;
    }

    private static bool TryParseRepartitionString(string raw, out double value)
    {
        value = 0;
        var t = raw.Replace('\u00a0', ' ').Trim();
        if (t.Length == 0) return false;
        if (t is "-" or "—" or "--" or "–") return false;
        if (t.Equals("n/a", StringComparison.OrdinalIgnoreCase)) return false;

        var m = PercentRegex.Match(t);
        if (m.Success)
        {
            var n = double.Parse(m.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            if (double.IsFinite(n))
            {
                value = n / 100.0;
                return true;
            }

            return false;
        }

        var normalized = t.Replace(" ", "").Replace(',', '.');
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && double.IsFinite(parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }
}
