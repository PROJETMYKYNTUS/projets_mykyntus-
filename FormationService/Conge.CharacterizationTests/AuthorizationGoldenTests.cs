using System.Net;
using Kyntus.Characterization.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Conge.CharacterizationTests;

public class AuthorizationGoldenTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthorizationGoldenTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(b => b.UseEnvironment("Testing")).CreateClient();
    }

    [Fact]
    public async Task Conges_list_without_token_returns_401()
    {
        var employeId = Guid.Parse("11111111-1111-4111-8111-111111111103");
        var response = await _client.GetAsync($"/api/conges/employe/{employeId}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Conges_list_with_test_jwt_returns_200_and_array()
    {
        KyntusTestJwt.AuthorizeClient(_client);
        var employeId = Guid.Parse("11111111-1111-4111-8111-111111111103");
        var response = await _client.GetAsync($"/api/conges/employe/{employeId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await GoldenJson.ReadAsync(response);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, json.RootElement.ValueKind);
    }
}
