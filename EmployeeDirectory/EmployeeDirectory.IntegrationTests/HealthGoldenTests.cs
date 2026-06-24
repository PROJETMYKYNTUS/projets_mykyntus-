using System.Net;
using Kyntus.Characterization.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EmployeeDirectory.CharacterizationTests;

public class HealthGoldenTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthGoldenTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(b => b.UseEnvironment("Testing")).CreateClient();
    }

    [Fact]
    public async Task Health_returns_golden_contract()
    {
        var response = await _client.GetAsync("/api/directory/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await GoldenJson.ReadAsync(response);
        GoldenJson.AssertStringProperty(json.RootElement, "status", "healthy");
        GoldenJson.AssertStringProperty(json.RootElement, "service", "employee-directory");
    }
}
