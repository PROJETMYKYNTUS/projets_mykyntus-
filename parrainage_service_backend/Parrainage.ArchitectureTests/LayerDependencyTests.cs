using NetArchTest.Rules;

namespace Parrainage.ArchitectureTests;

public class LayerDependencyTests
{
    private const string DomainNs = "Parrainage.Domain";
    private const string ApplicationNs = "Parrainage.Application";
    private const string InfrastructureNs = "Parrainage.Infrastructure";
    private const string ApiNs = "Parrainage.API";

    [Fact]
    public void Domain_should_not_reference_other_layers()
    {
        var result = Types.InAssembly(typeof(Domain.Entities.ReferralPositionMode).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNs, InfrastructureNs, ApiNs)
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_should_not_reference_Infrastructure_or_API()
    {
        var result = Types.InAssembly(typeof(Application.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNs, ApiNs)
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Infrastructure_should_not_reference_API()
    {
        var result = Types.InAssembly(typeof(Infrastructure.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNs)
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void API_Controllers_should_not_reference_Infrastructure()
    {
        var result = Types.InAssembly(typeof(API.Controllers.HealthController).Assembly)
            .That()
            .ResideInNamespace($"{ApiNs}.Controllers")
            .And()
            .DoNotResideInNamespace($"{ApiNs}.Controllers.Dev")
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNs)
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }
}
