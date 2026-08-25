using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Prime.Application.DTOs;

namespace Prime.Application.Rollover;

public sealed class PrimeSaisieRolloverMergeResult
{
    public string MergedJson { get; init; } = "{}";
    public int LinesCarried { get; init; }
    public IReadOnlyList<string> LinesNew { get; init; } = [];
    public IReadOnlyList<string> LinesDropped { get; init; } = [];
    public int UnconfirmedCarriedLines { get; init; }
}

/// <summary>
/// Fusionne une saisie source vers une cible par stableId, avec marqueurs de reconduction.
/// </summary>
public static class PrimeSaisieRolloverMerger
{
    private static readonly string[] ParameterSuffixes =
    [
        "kpiPointMin",
        "kpiPointMax",
        "kpiChallenge",
    ];

    private static readonly string[] MeasureSuffixes =
    [
        "resultatPrime",
        "resultatChallenge",
        "bonusAtteintPrime",
        "bonusAtteintChallenge",
        "montantPrime",
        "montantChallenge",
    ];

    private static readonly string[] NeverCarrySuffixes =
    [
        "ponderationPrime",
        "ponderationChallenge",
    ];

    public static PrimeSaisieRolloverMergeResult MergeTemplatePayload(
        string? sourceJson,
        string? schemaJson,
        string sourcePeriod,
        IReadOnlyDictionary<string, (decimal? Prime, decimal? Challenge)>? resolvedPonderations = null)
    {
        var schemaStableIds = ParseSchemaStableIds(schemaJson);
        var sourceRoot = ParseObject(sourceJson);
        var sourceLignes = sourceRoot?["lignes"] as JsonObject ?? new JsonObject();

        var targetRoot = new JsonObject
        {
            ["mode"] = sourceRoot?["mode"]?.GetValue<string>() ?? "template",
            ["templateFormatVersion"] = sourceRoot?["templateFormatVersion"]?.DeepClone(),
            ["fileName"] = sourceRoot?["fileName"]?.DeepClone(),
            ["contractsOrder"] = sourceRoot?["contractsOrder"]?.DeepClone(),
            ["lignes"] = new JsonObject(),
        };
        var targetLignes = (JsonObject)targetRoot["lignes"]!;

        var carried = 0;
        var linesNew = new List<string>();
        var linesDropped = new List<string>();
        var unconfirmed = 0;

        foreach (var stableId in schemaStableIds)
        {
            if (!sourceLignes.TryGetPropertyValue(stableId, out var sourceLine) || sourceLine is not JsonObject srcObj)
            {
                linesNew.Add(stableId);
                continue;
            }

            var merged = MergeLine(srcObj.DeepClone().AsObject(), sourcePeriod, resolvedPonderations);
            if (merged.HasMeasures)
            {
                carried++;
                if (!merged.Confirmed) unconfirmed++;
            }

            targetLignes[stableId] = merged.Line;
        }

        foreach (var prop in sourceLignes)
        {
            if (!schemaStableIds.Contains(prop.Key))
                linesDropped.Add(prop.Key);
        }

        return new PrimeSaisieRolloverMergeResult
        {
            MergedJson = targetRoot.ToJsonString(),
            LinesCarried = carried,
            LinesNew = linesNew,
            LinesDropped = linesDropped,
            UnconfirmedCarriedLines = unconfirmed,
        };
    }

    public static int CountUnconfirmedCarriedLines(string? json)
    {
        var root = ParseObject(json);
        var lignes = root?["lignes"] as JsonObject;
        if (lignes is null) return 0;
        var count = 0;
        foreach (var line in lignes)
        {
            if (line.Value is not JsonObject lo) continue;
            if (!IsCarriedUnconfirmed(lo)) continue;
            if (LineHasMeasures(lo)) count++;
        }

        return count;
    }

    private static (JsonObject Line, bool HasMeasures, bool Confirmed) MergeLine(
        JsonObject line,
        string sourcePeriod,
        IReadOnlyDictionary<string, (decimal? Prime, decimal? Challenge)>? resolvedPonderations)
    {
        var stableId = line["stableId"]?.GetValue<string>()?.Trim() ?? "";
        var hasMeasures = false;

        foreach (var prop in line.ToList())
        {
            var name = prop.Key;
            if (name is "carriedFrom" or "carriedConfirmed") continue;

            if (name == "repartitionRdv")
                continue;

            if (IsPonderationKey(name))
            {
                line.Remove(name);
                continue;
            }

            if (IsMeasureKey(name) && JsonNodeHasValue(prop.Value))
                hasMeasures = true;
        }

        if (resolvedPonderations is not null && stableId.Length > 0 &&
            resolvedPonderations.TryGetValue(stableId, out var pond))
        {
            ApplyResolvedPonderations(line, pond.Prime, pond.Challenge);
        }

        if (hasMeasures)
        {
            line["carriedFrom"] = sourcePeriod;
            line["carriedConfirmed"] = false;
        }
        else
        {
            line.Remove("carriedFrom");
            line.Remove("carriedConfirmed");
        }

        return (line, hasMeasures, false);
    }

    private static void ApplyResolvedPonderations(JsonObject line, decimal? prime, decimal? challenge)
    {
        var sectorIndices = line.Select(p => p.Key)
            .Select(ParseSectorIndex)
            .Where(i => i >= 0)
            .Distinct()
            .ToList();
        if (sectorIndices.Count == 0)
        {
            sectorIndices.Add(0);
        }

        foreach (var i in sectorIndices)
        {
            if (prime.HasValue)
                line[$"secteur_{i}_ponderationPrime"] = (double)prime.Value;
            if (challenge.HasValue)
                line[$"secteur_{i}_ponderationChallenge"] = (double)challenge.Value;
        }
    }

    private static bool IsCarriedUnconfirmed(JsonObject line)
    {
        if (!line.TryGetPropertyValue("carriedFrom", out var cf) || cf is null) return false;
        if (!line.TryGetPropertyValue("carriedConfirmed", out var cc)) return true;
        if (cc is JsonValue jv && jv.TryGetValue(out bool b)) return !b;
        return true;
    }

    private static bool LineHasMeasures(JsonObject line)
    {
        foreach (var prop in line)
        {
            if (IsMeasureKey(prop.Key) && JsonNodeHasValue(prop.Value))
                return true;
        }

        return false;
    }

    private static bool IsMeasureKey(string key)
    {
        if (key.Contains("_custom_", StringComparison.OrdinalIgnoreCase)) return true;
        return MeasureSuffixes.Any(s => key.EndsWith("_" + s, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPonderationKey(string key) =>
        NeverCarrySuffixes.Any(s => key.EndsWith("_" + s, StringComparison.OrdinalIgnoreCase));

    private static int ParseSectorIndex(string key)
    {
        const string prefix = "secteur_";
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return -1;
        var rest = key[prefix.Length..];
        var idx = rest.IndexOf('_');
        if (idx <= 0) return -1;
        return int.TryParse(rest[..idx], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : -1;
    }

    private static bool JsonNodeHasValue(JsonNode? node)
    {
        if (node is null) return false;
        if (node is JsonValue jv)
        {
            if (jv.TryGetValue(out string? s)) return !string.IsNullOrWhiteSpace(s);
            if (jv.TryGetValue(out double d)) return double.IsFinite(d);
            if (jv.TryGetValue(out int i)) return true;
            if (jv.TryGetValue(out long l)) return true;
        }

        return false;
    }

    private static HashSet<string> ParseSchemaStableIds(string? schemaJson)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var root = JsonNode.Parse(schemaJson ?? "{}");
            if (root?["lines"] is not JsonArray lines) return set;
            foreach (var line in lines)
            {
                var sid = line?["stableId"]?.GetValue<string>()?.Trim();
                if (!string.IsNullOrEmpty(sid)) set.Add(sid);
            }
        }
        catch
        {
            /* ignore */
        }

        return set;
    }

    private static JsonObject? ParseObject(string? json)
    {
        try
        {
            return JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) as JsonObject;
        }
        catch
        {
            return null;
        }
    }
}
