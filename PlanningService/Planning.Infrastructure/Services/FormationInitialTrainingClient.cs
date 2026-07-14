using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Planning.Infrastructure.Services;

public interface IFormationInitialTrainingClient
{
    Task<bool> TryCreateInitialPathAsync(
        Guid employeeId,
        string employeeName,
        DateTime dateDebut,
        DateTime dateFinPrevue,
        CancellationToken ct = default);
}

/// <summary>
/// Client HTTP vers Formation pour créer un parcours initiale (ex. après import enFormation).
/// </summary>
public sealed class FormationInitialTrainingClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    ILogger<FormationInitialTrainingClient> logger) : IFormationInitialTrainingClient
{
    public async Task<bool> TryCreateInitialPathAsync(
        Guid employeeId,
        string employeeName,
        DateTime dateDebut,
        DateTime dateFinPrevue,
        CancellationToken ct = default)
    {
        if (employeeId == Guid.Empty) return false;

        try
        {
            var client = CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/formations/initial-paths")
            {
                Content = JsonContent.Create(new
                {
                    employeeId,
                    employeeName,
                    dateDebut,
                    dateFinPrevue,
                }),
            };
            AttachAuth(request);

            var response = await client.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
                return true;

            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning(
                "Création parcours formation initiale échouée pour {EmployeeId} ({Status}): {Body}",
                employeeId,
                response.StatusCode,
                body);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Formation indisponible pour créer le parcours de {EmployeeId}.", employeeId);
            return false;
        }
    }

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient("FormationSync");
        var baseUrl = configuration["Formation:BaseUrl"] ?? "http://kyntus_formation_backend:8080/";
        if (!baseUrl.EndsWith('/'))
            baseUrl += "/";
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private void AttachAuth(HttpRequestMessage request)
    {
        var auth = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(auth))
            request.Headers.TryAddWithoutValidation("Authorization", auth);
    }
}
