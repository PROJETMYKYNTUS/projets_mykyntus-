using System.Net;
using System.Text;
using System.Text.Json;
using Auth.Application.DTOs;
using Kyntus.Characterization.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AuthService.CharacterizationTests;

/// <summary>
/// Contrat de validation login (sans dépendance DB — fige le comportement HTTP).
/// </summary>
public class LoginValidationGoldenTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Login_with_empty_body_returns_400()
    {
        var client = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Testing"))
            .CreateClient();

        var response = await client.PostAsync(
            "/api/auth/login",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await GoldenJson.ReadAsync(response);
        GoldenJson.AssertPropertyExists(json.RootElement, "errors");
    }

    [Fact]
    public async Task Login_with_missing_password_returns_400()
    {
        var client = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Testing"))
            .CreateClient();

        var body = JsonSerializer.Serialize(new { email = "rh@kyntus.ma" });
        var response = await client.PostAsync(
            "/api/auth/login",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
