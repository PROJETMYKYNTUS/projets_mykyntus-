using System.Net;
using Kyntus.Characterization.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Formation.CharacterizationTests;

public class FormationsListGoldenTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public FormationsListGoldenTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(b => b.UseEnvironment("Testing")).CreateClient();
    }

    [Fact]
    public async Task GetFormations_returns_200_and_array()
    {
        var response = await _client.GetAsync("/api/formations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await GoldenJson.ReadAsync(response);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, json.RootElement.ValueKind);
    }
}
