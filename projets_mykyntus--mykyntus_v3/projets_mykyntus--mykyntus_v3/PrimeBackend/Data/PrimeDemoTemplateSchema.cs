using System.Text.Json;

namespace PrimeBackend.Data;

/// <summary>Gabarit grille minimal (RACC + SAV) pour brouillons de démo sans import Excel.</summary>
public static class PrimeDemoTemplateSchema
{
    private static readonly string EmptySectorDefaults = """
        {
          "resultatPrime": "",
          "kpiPointMin": "",
          "kpiPointMax": "",
          "ponderationPrime": "",
          "bonusAtteintPrime": "",
          "montantPrime": "",
          "resultatChallenge": "",
          "kpiChallenge": "",
          "ponderationChallenge": "",
          "bonusAtteintChallenge": "",
          "montantChallenge": ""
        }
        """;

    public static string MinimalRaccSavJson(string? fileName = null, string? sheetName = null) =>
        $$"""
        {
          "templateFormatVersion": 1,
          "fileName": "{{fileName ?? "grille-prime-demo.xlsx"}}",
          "parsedAt": "2026-04-01T00:00:00.000Z",
          "sheetName": "{{sheetName ?? "Grille"}}",
          "contractsOrder": ["RACC", "SAV"],
          "lines": [
            {
              "stableId": "demo-racc-1",
              "contract": "RACC",
              "indicator": "Indicateur RACC",
              "bareme": "",
              "groupe": "",
              "repartitionRdv": "100",
              "sourceRowIndex": 2,
              "secteurs": [
                {
                  "sectorIndex": 0,
                  "label": "Secteur 1",
                  "defaults": {{EmptySectorDefaults}},
                  "gridStartCol": 6
                }
              ]
            },
            {
              "stableId": "demo-sav-1",
              "contract": "SAV",
              "indicator": "Indicateur SAV",
              "bareme": "",
              "groupe": "",
              "repartitionRdv": "100",
              "sourceRowIndex": 3,
              "secteurs": [
                {
                  "sectorIndex": 0,
                  "label": "Secteur 1",
                  "defaults": {{EmptySectorDefaults}},
                  "gridStartCol": 6
                }
              ]
            }
          ]
        }
        """;

    public static bool IsObsoleteSchemaJson(string? schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson)) return true;
        var raw = schemaJson.Trim();
        if (raw is "{}" or "null") return true;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.TryGetProperty("fields", out _) && !root.TryGetProperty("lines", out _))
                return true;
            if (!root.TryGetProperty("lines", out var lines))
                return true;
            return lines.ValueKind != JsonValueKind.Array || lines.GetArrayLength() == 0;
        }
        catch
        {
            return true;
        }
    }
}
