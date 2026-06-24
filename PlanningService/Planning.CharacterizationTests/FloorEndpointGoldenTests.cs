using System.Net;
using System.Text.Json;
using Kyntus.Characterization.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PlanningService.CharacterizationTests;

public class FloorEndpointGoldenTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public FloorEndpointGoldenTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(b => b.UseEnvironment("Testing")).CreateClient();
    }

    [Fact]
    public async Task GetAllFloors_returns_200_and_empty_array_when_no_data()
    {
        var response = await _client.GetAsync("/api/floor");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await GoldenJson.ReadAsync(response);
        GoldenJson.AssertArrayEmpty(json.RootElement);
    }
}
