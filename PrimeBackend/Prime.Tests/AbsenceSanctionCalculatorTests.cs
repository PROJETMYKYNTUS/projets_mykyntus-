using Prime.Infrastructure.Services;

namespace Prime.Tests;

public class AbsenceSanctionCalculatorTests
{
    [Fact]
    public void ComputeSanction_UsesTotalAndDivisor()
    {
        var sanction = PrimeAbsenceSanctionCalculator.ComputeSanction(2600m, 2, 26);
        Assert.Equal(200m, sanction);
    }

    [Fact]
    public void ComputeSanction_ReturnsZero_WhenNoAbsences()
    {
        Assert.Equal(0m, PrimeAbsenceSanctionCalculator.ComputeSanction(2600m, 0, 26));
    }

    [Fact]
    public void ComputeNetPayable_SubtractsSanctionAndAddsRegularization()
    {
        var net = PrimeAbsenceSanctionCalculator.ComputeNetPayable(2600m, 200m, -50m);
        Assert.Equal(2350m, net);
    }

    [Fact]
    public void ComputeNetPayable_AllowsNegativeRegularization()
    {
        var net = PrimeAbsenceSanctionCalculator.ComputeNetPayable(1000m, 100m, -25m);
        Assert.Equal(875m, net);
    }
}
