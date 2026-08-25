using System.Text.Json;
using System.Text.RegularExpressions;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Services;

/// <summary>
/// Statut de remplissage partie service : format legacy (cible/réalisé) ou v2 (plafonds + lignes aplatis secteur_*).
/// </summary>
public static class PrimeServiceFicheStatusHelper
{
    private static readonly string[] SectorCoreSuffixes =
    [
        "resultatPrime",
        "kpiPointMin",
        "kpiPointMax",
        "ponderationPrime",
        "bonusAtteintPrime",
        "montantPrime",
        "resultatChallenge",
        "kpiChallenge",
        "ponderationChallenge",
        "bonusAtteintChallenge",
        "montantChallenge",
    ];

    /// <summary>Champs optionnels (vide autorisé) — aligné sur fiches Excel et validation frontend.</summary>
    private static readonly HashSet<string> OptionalSectorCoreSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "resultatChallenge",
        "kpiChallenge",
        "ponderationChallenge",
        "bonusAtteintChallenge",
        "montantChallenge",
        "bonusAtteintPrime",
        "montantPrime",
    };

    private static readonly Regex SectorIndexRegex = new(@"^secteur_(\d+)_", RegexOptions.Compiled);

    public static string ComputeFillingStatus(string serviceSaisieJson, IReadOnlyList<ServicePrimeIndicatorEntity> activeOrderedIndicators)
    {
        var active = activeOrderedIndicators.Where(i => i.IsActive).OrderBy(i => i.SortOrder).ToList();
        if (active.Count == 0)
            return "Complete";

        JsonDocument? doc = null;
        try
        {
            doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(serviceSaisieJson) ? "{}" : serviceSaisieJson);
        }
        catch
        {
            return "NotStarted";
        }

        using (doc)
        {
            var root = doc!.RootElement;
            if (HasUnconfirmedCarriedServiceRows(root))
                return "InProgress";

            if (!root.TryGetProperty("rows", out var rowsEl) || rowsEl.ValueKind != JsonValueKind.Array)
                return "NotStarted";

            var rowsArr = rowsEl.EnumerateArray().ToList();
            if (rowsArr.Count == 0)
                return "NotStarted";

            var explicitV2 = root.TryGetProperty("formatVersion", out var fvEl) && fvEl.TryGetInt32(out var fv) && fv >= 2;
            var anyDynamic = rowsArr.Any(RowElementHasDynamicShape);

            if (explicitV2 || anyDynamic)
                return ComputeDynamic(root, rowsArr, active);

            return ComputeLegacy(rowsArr, active);
        }
    }

    private static bool RowElementHasDynamicShape(JsonElement row)
    {
        if (row.ValueKind != JsonValueKind.Object)
            return false;
        foreach (var p in row.EnumerateObject())
        {
            if (p.Name.StartsWith("secteur_", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string ComputeLegacy(IReadOnlyList<JsonElement> rowsArr, List<ServicePrimeIndicatorEntity> active)
    {
        static bool Filled(string? s) => !string.IsNullOrWhiteSpace(s);

        var anyValue = false;
        var allComplete = true;
        foreach (var ind in active)
        {
            JsonElement row = default;
            var found = false;
            foreach (var el in rowsArr)
            {
                if (el.ValueKind == JsonValueKind.Object && TryGetGuid(el, "indicatorId", out var gid) && gid == ind.Id)
                {
                    row = el;
                    found = true;
                    break;
                }
            }

            string? c = null;
            string? r = null;
            if (found && row.ValueKind == JsonValueKind.Object)
            {
                if (row.TryGetProperty("cible", out var cEl))
                    c = cEl.ValueKind == JsonValueKind.String ? cEl.GetString() : cEl.ToString();
                if (row.TryGetProperty("realise", out var rEl))
                    r = rEl.ValueKind == JsonValueKind.String ? rEl.GetString() : rEl.ToString();
            }

            if (Filled(c) || Filled(r))
                anyValue = true;
            if (!Filled(c) || !Filled(r))
                allComplete = false;
        }

        if (!anyValue)
            return "NotStarted";
        return allComplete ? "Complete" : "InProgress";
    }

    private static string ComputeDynamic(JsonElement root, IReadOnlyList<JsonElement> rowsArr, List<ServicePrimeIndicatorEntity> active)
    {
        static bool FilledPlafond(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.String)
                return !string.IsNullOrWhiteSpace(el.GetString());
            if (el.ValueKind == JsonValueKind.Number)
                return true;
            return false;
        }

        var plafPrimeOk = root.TryGetProperty("plafondPrime", out var pp) && FilledPlafond(pp);
        var plafChOk = root.TryGetProperty("plafondChallenge", out var pc) && FilledPlafond(pc);

        var rowByIndicator = new Dictionary<Guid, JsonElement>();
        foreach (var row in rowsArr)
        {
            if (row.ValueKind != JsonValueKind.Object)
                continue;
            if (!TryGetGuid(row, "indicatorId", out var gid))
                continue;
            rowByIndicator[gid] = row;
        }

        var anyValue = false;

        foreach (var ind in active)
        {
            if (!rowByIndicator.TryGetValue(ind.Id, out var row))
                continue;
            if (RowElementHasDynamicShape(row))
            {
                if (AnySectorCoreFilled(row))
                    anyValue = true;
            }
        }

        if (plafPrimeOk || plafChOk)
            anyValue = true;

        if (!anyValue)
            return "NotStarted";

        var allComplete = plafPrimeOk && plafChOk;
        if (!allComplete)
            return "InProgress";

        foreach (var ind in active)
        {
            if (!rowByIndicator.TryGetValue(ind.Id, out var row) || !RowElementHasDynamicShape(row))
            {
                allComplete = false;
                break;
            }

            if (!AllSectorsCompleteForRow(row))
            {
                allComplete = false;
                break;
            }
        }

        return allComplete ? "Complete" : "InProgress";
    }

    private static bool AnySectorCoreFilled(JsonElement row)
    {
        foreach (var p in row.EnumerateObject())
        {
            if (!SectorIndexRegex.IsMatch(p.Name))
                continue;
            if (!SectorCoreSuffixes.Any(sfx => p.Name.EndsWith("_" + sfx, StringComparison.OrdinalIgnoreCase)))
                continue;
            if (JsonValueFilled(p.Value))
                return true;
        }

        return false;
    }

    private static bool AllSectorsCompleteForRow(JsonElement row)
    {
        var indices = new HashSet<int>();
        foreach (var p in row.EnumerateObject())
        {
            var m = SectorIndexRegex.Match(p.Name);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var idx))
                indices.Add(idx);
        }

        if (indices.Count == 0)
            return false;

        foreach (var i in indices)
        {
            foreach (var sfx in SectorCoreSuffixes)
            {
                if (OptionalSectorCoreSuffixes.Contains(sfx))
                    continue;
                var key = $"secteur_{i}_{sfx}";
                if (!row.TryGetProperty(key, out var el) || !JsonValueFilled(el))
                    return false;
            }
        }

        return true;
    }

    private static bool JsonValueFilled(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => !string.IsNullOrWhiteSpace(el.GetString()),
            JsonValueKind.Number => true,
            JsonValueKind.True or JsonValueKind.False => true,
            _ => false,
        };
    }

    private static bool TryGetGuid(JsonElement obj, string prop, out Guid guid)
    {
        guid = default;
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(prop, out var el))
            return false;
        var s = el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
        return Guid.TryParse(s, out guid);
    }

    private static bool HasUnconfirmedCarriedServiceRows(JsonElement root)
    {
        if (root.TryGetProperty("carriedFrom", out _) &&
            (!root.TryGetProperty("carriedConfirmed", out var cc) ||
             cc.ValueKind != JsonValueKind.True))
            return true;

        if (!root.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object) continue;
            if (!row.TryGetProperty("carriedFrom", out _)) continue;
            if (!row.TryGetProperty("carriedConfirmed", out var confirmed) || confirmed.ValueKind != JsonValueKind.True)
                return true;
        }

        return false;
    }
}
