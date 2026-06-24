using NetArchTest.Rules;

namespace EmployeeDirectory.ArchitectureTests;

public class LayerDependencyTests
{
    private const string DomainNs = "EmployeeDirectory.Domain";
    private const string ApplicationNs = "EmployeeDirectory.Application";
    private const string InfrastructureNs = "EmployeeDirectory.Infrastructure";

    [Fact]
    public void Domain_should_not_reference_Infrastructure_or_Application()
    {
        var result = Types.InAssembly(typeof(Domain.Entities.Employee).Assembly)
            .Should()
            .NotHaveDependencyOn(InfrastructureNs)
            .And()
            .NotHaveDependencyOn(ApplicationNs)
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_should_not_reference_Infrastructure()
    {
        var result = Types.InAssembly(typeof(Application.Behaviors.ValidationBehavior<,>).Assembly)
            .Should()
            .NotHaveDependencyOn(InfrastructureNs)
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Domain_should_not_reference_EntityFramework()
    {
        var result = Types.InAssembly(typeof(Domain.Entities.Employee).Assembly)
            .Should()
            .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Handlers_should_reside_in_Application()
    {
        var result = Types.InAssembly(typeof(Application.Queries.Health.GetHealthQueryHandler).Assembly)
            .That()
            .HaveNameEndingWith("Handler")
            .Should()
            .ResideInNamespace(ApplicationNs)
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }
}
