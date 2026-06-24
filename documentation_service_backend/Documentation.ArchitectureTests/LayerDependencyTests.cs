using NetArchTest.Rules;

namespace Documentation.ArchitectureTests;

public class LayerDependencyTests
{
    private const string DomainNs = "Documentation.Domain";
    private const string ApplicationNs = "Documentation.Application";
    private const string InfrastructureNs = "Documentation.Infrastructure";
    private const string ApiNs = "Documentation.API";

    [Fact]
    public void Domain_should_not_reference_other_layers()
    {
        var result = Types.InAssembly(typeof(Domain.Entities.Workflow).Assembly)
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
        var result = Types.InAssembly(typeof(API.Controllers.DocumentationDocumentsController).Assembly)
            .That()
            .ResideInNamespace($"{ApiNs}.Controllers")
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNs)
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }
}
