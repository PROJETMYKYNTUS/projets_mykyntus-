using Formation.Domain.Entities;
using Formation.Domain.Enums;
using Formation.Infrastructure.Services;
using Xunit;

namespace Formation.UnitTests;

public class AudienceResolverTests
{
    [Fact]
    public void MatchStructure_PoleSelection_MatchesEmployeeWithSamePole_EvenIfServiceDiffers()
    {
        var employee = new EmployeAnnuaire
        {
            PoleId = "pole-1",
            CelluleId = "cell-a",
            ServiceId = "svc-x",
        };

        Assert.True(AudienceResolver.MatchStructure(employee, ["pole-1"]));
        Assert.False(AudienceResolver.MatchStructure(employee, ["pole-other"]));
    }

    [Fact]
    public void MatchStructure_ServiceSelection_MatchesOnlyThatService()
    {
        var employee = new EmployeAnnuaire
        {
            PoleId = "pole-1",
            ServiceId = "svc-x",
        };
        var other = new EmployeAnnuaire
        {
            PoleId = "pole-1",
            ServiceId = "svc-y",
        };

        Assert.True(AudienceResolver.MatchStructure(employee, ["svc-x"]));
        Assert.False(AudienceResolver.MatchStructure(other, ["svc-x"]));
    }

    [Fact]
    public void MatchStructure_LegacyStructureKeyAlone_Works()
    {
        var employee = new EmployeAnnuaire
        {
            StructureKey = "legacy-node",
        };

        Assert.True(AudienceResolver.MatchStructure(employee, ["legacy-node"]));
        Assert.False(AudienceResolver.MatchStructure(employee, ["other"]));
    }

    [Fact]
    public void Matches_MatchAny_RolesOrStructures()
    {
        var employee = new EmployeAnnuaire
        {
            Role = "Employee",
            PoleId = "pole-1",
            ServiceId = "svc-x",
        };

        Assert.True(AudienceResolver.Matches(
            employee,
            roles: ["Manager"],
            structures: ["pole-1"],
            users: [],
            CatalogAudienceMatchMode.MatchAny));

        Assert.True(AudienceResolver.Matches(
            employee,
            roles: ["Employee"],
            structures: ["pole-other"],
            users: [],
            CatalogAudienceMatchMode.MatchAny));

        Assert.False(AudienceResolver.Matches(
            employee,
            roles: ["Manager"],
            structures: ["pole-other"],
            users: [],
            CatalogAudienceMatchMode.MatchAny));
    }

    [Fact]
    public void Matches_MatchAll_RequiresRolesAndStructures()
    {
        var employee = new EmployeAnnuaire
        {
            Role = "Employee",
            PoleId = "pole-1",
            ServiceId = "svc-x",
        };

        Assert.True(AudienceResolver.Matches(
            employee,
            roles: ["Employee"],
            structures: ["pole-1"],
            users: [],
            CatalogAudienceMatchMode.MatchAll));

        Assert.False(AudienceResolver.Matches(
            employee,
            roles: ["Manager"],
            structures: ["pole-1"],
            users: [],
            CatalogAudienceMatchMode.MatchAll));

        Assert.False(AudienceResolver.Matches(
            employee,
            roles: ["Employee"],
            structures: ["pole-other"],
            users: [],
            CatalogAudienceMatchMode.MatchAll));
    }
}
