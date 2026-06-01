using PrimeBackend.Services;
using Xunit;

namespace PrimeBackend.Tests;

public sealed class PrimePlafondAndLineDecisionTests
{
    [Fact]
    public void ExtractPlafonds_reads_root_keys()
    {
        var json = """{"formatVersion":2,"plafondPrime":"1500","plafondChallenge":"800","rows":[]}""";
        var amounts = PrimeEmployeeFicheAmountService.ExtractPlafondsFromServiceSaisieJson(json);
        Assert.Equal(1500m, amounts.PrimeAmount);
        Assert.Equal(800m, amounts.ChallengeAmount);
        Assert.Equal(2300m, amounts.TotalAmount);
    }

    [Fact]
    public void HasNegativeFinancialValuesInServiceSaisieJson_detects_negative_montant()
    {
        var json = """{"formatVersion":2,"rows":[{"montantPrime":"-10","montantChallenge":"5"}]}""";
        var hasNegative = PrimeEmployeeFicheAmountService.HasNegativeFinancialValuesInServiceSaisieJson(json);
        Assert.True(hasNegative);
    }

    [Fact]
    public void HasNegativeFinancialValuesInServiceSaisieJson_detects_negative_plafond()
    {
        var json = """{"formatVersion":2,"plafondPrime":"-100","plafondChallenge":"25","rows":[{"montantPrime":"10"}]}""";
        var hasNegative = PrimeEmployeeFicheAmountService.HasNegativeFinancialValuesInServiceSaisieJson(json);
        Assert.True(hasNegative);
    }

    [Fact]
    public void HasNegativeFinancialValuesInServiceSaisieJson_accepts_non_negative_payload()
    {
        var json = """{"formatVersion":2,"plafondPrime":"100","plafondChallenge":"25","rows":[{"montantPrime":"10","montantChallenge":"2"}]}""";
        var hasNegative = PrimeEmployeeFicheAmountService.HasNegativeFinancialValuesInServiceSaisieJson(json);
        Assert.False(hasNegative);
    }

    [Theory]
    [InlineData("Pending", "Pending", "PendingReview")]
    [InlineData("Approved", "Pending", "PendingReview")]
    [InlineData("Pending", "Approved", "PendingReview")]
    [InlineData("Rejected", "Pending", "PendingReview")]
    [InlineData("Pending", "Rejected", "PendingReview")]
    [InlineData("Approved", "Approved", "Approved")]
    [InlineData("Rejected", "Approved", "LineRejected")]
    [InlineData("Approved", "Rejected", "LineRejected")]
    [InlineData("Rejected", "Rejected", "LineRejected")]
    public void DeriveLineStatus_dual_validation(string rh, string mgr, string expected)
    {
        var status = GlobalPoolLineDecisions.DeriveLineStatus(rh, mgr);
        Assert.Equal(expected, status);
    }
}
