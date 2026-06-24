using System.Net;
using Kyntus.Characterization.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DocumentationBackend.CharacterizationTests;

public class HealthEndpointGoldenTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointGoldenTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(b => b.UseEnvironment("Testing")).CreateClient();
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/healthz")]
    [InlineData("/api/documentation/health")]
    public async Task Health_endpoints_return_healthy_contract(string path)
    {
        var response = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await GoldenJson.ReadAsync(response);
        GoldenJson.AssertStringProperty(json.RootElement, "status", "Healthy");
        GoldenJson.AssertStringProperty(json.RootElement, "service", "documentation");
    }
}
