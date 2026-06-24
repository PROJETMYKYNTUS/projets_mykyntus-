using System.Net;
using Kyntus.Characterization.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PrimeBackend.CharacterizationTests;

public class AuthorizationGoldenTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthorizationGoldenTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(b => b.UseEnvironment("Testing")).CreateClient();
    }

    [Fact]
    public async Task Protected_endpoint_without_token_returns_401()
    {
        var response = await _client.GetAsync("/api/prime/allowances/types");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
