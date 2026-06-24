using NetArchTest.Rules;

namespace Planning.ArchitectureTests;

public class LayerDependencyTests
{
    private const string DomainNs = "Planning.Domain";
    private const string ApplicationNs = "Planning.Application";
    private const string InfrastructureNs = "Planning.Infrastructure";
    private const string ApiNs = "Planning.API";

    [Fact]
    public void Domain_should_not_reference_other_layers()
    {
        var result = Types.InAssembly(typeof(Domain.Entities.Floor).Assembly)
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
        var result = Types.InAssembly(typeof(Planning.API.Controllers.RolesController).Assembly)
            .That()
            .ResideInNamespace($"{ApiNs}.Controllers")
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNs)
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }
}
