using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EmployeeDirectory.IntegrationTests;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
        }).CreateClient();
    }

    [Fact]
    public async Task Health_returns_200_and_clean_architecture_marker()
    {
        var response = await _client.GetAsync("/api/directory/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("healthy", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("employee-directory", json, StringComparison.OrdinalIgnoreCase);
    }
}
