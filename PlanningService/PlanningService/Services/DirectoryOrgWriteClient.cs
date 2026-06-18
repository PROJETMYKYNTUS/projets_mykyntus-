using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;

namespace PlanningService.Services;

public interface IDirectoryOrgWriteClient
{
    Task<string> CreatePoleAsync(string name, CancellationToken ct = default);
    Task<string> CreateCelluleAsync(string poleDirectoryId, string name, CancellationToken ct = default);
    Task<string> CreateServiceAsync(string celluleDirectoryId, string name, CancellationToken ct = default);

    Task<bool> AssignChefDeProjetAsync(string poleDirectoryId, Guid employeeGuid, CancellationToken ct = default);
    Task<bool> AssignSuperviseurAsync(string celluleDirectoryId, Guid employeeGuid, CancellationToken ct = default);
    Task<bool> AssignReferentTechniqueAsync(string serviceDirectoryId, Guid employeeGuid, CancellationToken ct = default);
}

public sealed class DirectoryOrgWriteClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    ILogger<DirectoryOrgWriteClient> logger) : IDirectoryOrgWriteClient
{
    public Task<string> CreatePoleAsync(string name, CancellationToken ct = default) =>
        PostNodeAsync("api/directory/org/structure/poles", new { name = name.Trim() }, ct);

    public Task<string> CreateCelluleAsync(string poleDirectoryId, string name, CancellationToken ct = default) =>
        PostNodeAsync(
            $"api/directory/org/structure/poles/{Uri.EscapeDataString(poleDirectoryId)}/cellules",
            new { name = name.Trim() },
            ct);

    public Task<string> CreateServiceAsync(string celluleDirectoryId, string name, CancellationToken ct = default) =>
        PostNodeAsync(
            $"api/directory/org/structure/cellules/{Uri.EscapeDataString(celluleDirectoryId)}/services",
            new { name = name.Trim() },
            ct);

    public Task<bool> AssignChefDeProjetAsync(string poleDirectoryId, Guid employeeGuid, CancellationToken ct = default) =>
        PostAssignmentAsync(
            $"api/directory/org/assignments/ChefDeProjet/{Uri.EscapeDataString(poleDirectoryId)}",
            employeeGuid,
            ct);

    public Task<bool> AssignSuperviseurAsync(string celluleDirectoryId, Guid employeeGuid, CancellationToken ct = default) =>
        PostAssignmentAsync(
            $"api/directory/org/assignments/Superviseur/{Uri.EscapeDataString(celluleDirectoryId)}",
            employeeGuid,
            ct);

    public Task<bool> AssignReferentTechniqueAsync(string serviceDirectoryId, Guid employeeGuid, CancellationToken ct = default) =>
        PostAssignmentAsync(
            $"api/directory/org/assignments/ReferentTechnique/{Uri.EscapeDataString(serviceDirectoryId)}",
            employeeGuid,
            ct);

    private async Task<string> PostNodeAsync(string path, object body, CancellationToken ct)
    {
        var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        AttachAuth(request);

        var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Directory org create {Path} failed {Status}: {Body}", path, response.StatusCode, err);
            throw new InvalidOperationException($"Création organisationnelle échouée ({response.StatusCode}).");
        }

        var created = await response.Content.ReadFromJsonAsync<DirectoryNodeJson>(cancellationToken: ct);
        if (created is null || string.IsNullOrWhiteSpace(created.Id))
            throw new InvalidOperationException("Réponse Directory invalide après création org.");

        return created.Id;
    }

    private async Task<bool> PostAssignmentAsync(string path, Guid employeeGuid, CancellationToken ct)
    {
        var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new { employeeId = employeeGuid.ToString("D") }),
        };
        AttachAuth(request);

        var response = await client.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
            return true;

        var err = await response.Content.ReadAsStringAsync(ct);
        logger.LogWarning("Directory assignment {Path} failed {Status}: {Body}", path, response.StatusCode, err);
        return false;
    }

    private HttpClient CreateClient()
    {
        var baseUrl = configuration["Directory:BaseUrl"]?.Trim()
            ?? "http://employee-directory-backend:8080/";
        if (!baseUrl.EndsWith('/')) baseUrl += "/";

        var client = httpClientFactory.CreateClient("DirectorySync");
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(15);
        return client;
    }

    private void AttachAuth(HttpRequestMessage request)
    {
        var authHeader = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authHeader))
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);
    }

    private sealed class DirectoryNodeJson
    {
        public string Id { get; set; } = "";
    }
}
