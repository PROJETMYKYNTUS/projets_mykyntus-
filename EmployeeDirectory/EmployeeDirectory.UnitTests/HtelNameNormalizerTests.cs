using EmployeeDirectory.Infrastructure.Services;

namespace EmployeeDirectory.UnitTests;

public class HtelNameNormalizerTests
{
    [Theory]
    [InlineData("AARAB Fatima Zahra", "Fatima Zahra", "AARAB", true)]
    [InlineData("AARAB Fatima Zahra", "AARAB", "Fatima Zahra", true)]
    [InlineData("Dupont Jean", "Jean", "Dupont", true)]
    [InlineData("Dupont Jean", "Paul", "Dupont", false)]
    public void Employee_keys_match_technicien(string technicien, string first, string last, bool expectMatch)
    {
        var techKey = HtelNameNormalizer.TechnicienKey(technicien);
        var keys = HtelNameNormalizer.EmployeeNameKeys(first, last).ToHashSet();
        Assert.Equal(expectMatch, keys.Contains(techKey));
    }

    [Fact]
    public void Normalize_strips_accents_and_case()
    {
        Assert.Equal("garcon", HtelNameNormalizer.Normalize("  Garçon  "));
    }
}
