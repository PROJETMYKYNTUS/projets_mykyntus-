using EmployeeDirectory.Application.Queries.Health;

namespace EmployeeDirectory.UnitTests;

public class GetHealthQueryHandlerTests
{
    [Fact]
    public async Task Handle_returns_healthy_status()
    {
        var handler = new GetHealthQueryHandler();
        var result = await handler.Handle(new GetHealthQuery(), CancellationToken.None);
        Assert.Equal("healthy", result.Status);
        Assert.Equal("employee-directory", result.Service);
    }
}
