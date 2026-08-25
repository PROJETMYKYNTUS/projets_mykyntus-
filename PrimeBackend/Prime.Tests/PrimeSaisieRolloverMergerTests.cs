using Prime.Application.Rollover;

namespace Prime.Tests;

public class PrimeSaisieRolloverMergerTests
{
    private const string Schema = """
        {
          "lines": [
            { "stableId": "line-a", "contract": "RACC", "indicator": "KPI A" },
            { "stableId": "line-b", "contract": "SAV", "indicator": "KPI B" }
          ]
        }
        """;

    private const string Source = """
        {
          "mode": "template",
          "lignes": {
            "line-a": {
              "stableId": "line-a",
              "repartitionRdv": 1,
              "secteur_0_kpiPointMin": 80,
              "secteur_0_resultatPrime": 95,
              "secteur_0_ponderationPrime": 10
            },
            "line-b": {
              "stableId": "line-b",
              "secteur_0_resultatPrime": 70
            },
            "line-old": { "stableId": "line-old", "secteur_0_resultatPrime": 50 }
          }
        }
        """;

    [Fact]
    public void MergeTemplatePayload_carries_parameters_silently_and_marks_measures()
    {
        var pond = new Dictionary<string, (decimal? Prime, decimal? Challenge)>
        {
            ["line-a"] = (25m, 15m),
        };

        var result = PrimeSaisieRolloverMerger.MergeTemplatePayload(Source, Schema, "2026-07", pond);

        Assert.Equal(2, result.LinesCarried);
        Assert.DoesNotContain("line-a", result.LinesNew);
        Assert.DoesNotContain("line-b", result.LinesNew);
        Assert.Contains("line-old", result.LinesDropped);
        Assert.Equal(2, result.UnconfirmedCarriedLines);

        using var doc = System.Text.Json.JsonDocument.Parse(result.MergedJson);
        var lineA = doc.RootElement.GetProperty("lignes").GetProperty("line-a");
        Assert.Equal("2026-07", lineA.GetProperty("carriedFrom").GetString());
        Assert.False(lineA.GetProperty("carriedConfirmed").GetBoolean());
        Assert.Equal(80, lineA.GetProperty("secteur_0_kpiPointMin").GetDouble());
        Assert.Equal(25, lineA.GetProperty("secteur_0_ponderationPrime").GetDouble());
    }

    [Fact]
    public void CountUnconfirmedCarriedLines_returns_count_when_not_confirmed()
    {
        var json = """
            {
              "lignes": {
                "line-a": { "carriedFrom": "2026-07", "carriedConfirmed": false, "secteur_0_resultatPrime": 1 }
              }
            }
            """;

        Assert.Equal(1, PrimeSaisieRolloverMerger.CountUnconfirmedCarriedLines(json));
    }
}
