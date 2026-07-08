using Prime.Domain.Entities;
using Prime.Infrastructure.Services;

namespace Prime.Tests;

public class PrimeEmployeeFicheAmountServiceTests
{
    [Fact]
    public void ResolveWorkflowDisplayAmounts_falls_back_to_plafonds_when_montants_missing()
    {
        var fiche = new EmployeePrimeServiceFiche
        {
            ServiceSaisieJson = """
                {
                  "plafondPrime": "1500",
                  "plafondChallenge": "400",
                  "rows": [
                    { "ponderationPrime": "100", "montantPrime": "" }
                  ]
                }
                """,
        };

        var amounts = PrimeEmployeeFicheAmountService.ResolveWorkflowDisplayAmounts(fiche);

        Assert.Equal(1500m, amounts.PrimeAmount);
        Assert.Equal(400m, amounts.ChallengeAmount);
        Assert.Equal(1900m, amounts.TotalAmount);
    }

    [Fact]
    public void ResolveWorkflowDisplayAmounts_prefers_calculated_montants_over_plafonds()
    {
        var fiche = new EmployeePrimeServiceFiche
        {
            ServiceSaisieJson = """
                {
                  "plafondPrime": "1500",
                  "plafondChallenge": "400",
                  "rows": [
                    { "montantPrime": "1200", "montantChallenge": "250" }
                  ]
                }
                """,
        };

        var amounts = PrimeEmployeeFicheAmountService.ResolveWorkflowDisplayAmounts(fiche);

        Assert.Equal(1200m, amounts.PrimeAmount);
        Assert.Equal(250m, amounts.ChallengeAmount);
        Assert.Equal(1450m, amounts.TotalAmount);
    }

    [Fact]
    public void ResolveWorkflowDisplayAmounts_uses_entity_columns_when_saisie_empty()
    {
        var fiche = new EmployeePrimeServiceFiche
        {
            PrimeAmount = 800m,
            ChallengeAmount = 100m,
            TotalAmount = 900m,
            ServiceSaisieJson = "{}",
        };

        var amounts = PrimeEmployeeFicheAmountService.ResolveWorkflowDisplayAmounts(fiche);

        Assert.Equal(800m, amounts.PrimeAmount);
        Assert.Equal(100m, amounts.ChallengeAmount);
        Assert.Equal(900m, amounts.TotalAmount);
    }
}
