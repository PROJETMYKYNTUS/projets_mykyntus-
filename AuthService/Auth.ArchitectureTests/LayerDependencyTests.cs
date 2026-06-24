using NetArchTest.Rules;

namespace Auth.ArchitectureTests;

public class LayerDependencyTests
{
    private const string DomainNs = "Auth.Domain";
    private const string ApplicationNs = "Auth.Application";
    private const string InfrastructureNs = "Auth.Infrastructure";
    private const string ApiNs = "Auth.API";

    [Fact]
    public void Domain_should_not_reference_other_layers()
    {
        var result = Types.InAssembly(typeof(Domain.Entities.User).Assembly)
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
}
