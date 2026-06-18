using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kyntus.Iam;

/// <summary>Propage le JWT de la requête entrante vers Employee Directory.</summary>
public sealed class DirectoryForwardAuthorizationHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var authHeader = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authHeader))
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);

        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>ReBAC via Employee Directory HTTP API (consommé par les modules projection).</summary>
public sealed class DirectoryHttpRebacClient(HttpClient http) : IRebacClient
{
    public async Task<bool> IsDescendantAsync(Guid viewerId, Guid targetId, CancellationToken ct = default)
    {
        var url = $"/api/directory/rebac/is-descendant?viewerId={viewerId}&targetId={targetId}";
        var resp = await http.GetFromJsonAsync<IsDescendantResponse>(url, ct);
        return resp?.IsDescendant == true;
    }

    public async Task<IReadOnlyList<string>> GetManagedNodeIdsAsync(Guid employeeId, string kind, CancellationToken ct = default)
    {
        var url =
            $"/api/directory/rebac/managed-nodes?employeeId={employeeId}&kind={Uri.EscapeDataString(kind)}";
        var resp = await http.GetFromJsonAsync<ManagedNodesResponse>(url, ct);
        return resp?.NodeIds ?? [];
    }

    private sealed record IsDescendantResponse(bool IsDescendant);
    private sealed record ManagedNodesResponse(IReadOnlyList<string> NodeIds);
}

/// <summary>Évalue les policies via POST /api/iam/evaluate (forward JWT utilisateur).</summary>
public sealed class DirectoryHttpPolicyEvaluator(HttpClient http) : IPolicyEvaluator
{
    public async Task<PolicyDecision> EvaluateAsync(PolicyRequest request, CancellationToken ct = default)
    {
        if (IsPrivilegedRole(request.Role))
            return new PolicyDecision(true);

        var resp = await http.PostAsJsonAsync("/api/iam/evaluate", new
        {
            action = request.Action,
            resourceType = request.ResourceType,
            resourceId = request.ResourceId,
        }, ct);

        if (!resp.IsSuccessStatusCode)
            return new PolicyDecision(false, $"Directory IAM HTTP {(int)resp.StatusCode}");

        var body = await resp.Content.ReadFromJsonAsync<EvaluateResponse>(cancellationToken: ct);
        return body is null
            ? new PolicyDecision(false, "Empty IAM response")
            : new PolicyDecision(body.Allowed, body.Reason);
    }

    private static bool IsPrivilegedRole(string role) =>
        string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, "RH", StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, "Audit", StringComparison.OrdinalIgnoreCase);

    private sealed record EvaluateResponse(bool Allowed, string? Reason);
}

public static class DirectoryHttpIamExtensions
{
    public static IServiceCollection AddKyntusIamViaDirectoryHttp(
        this IServiceCollection services,
        string directoryBaseUrl)
    {
        var baseUri = new Uri(directoryBaseUrl.TrimEnd('/') + "/");

        services.AddHttpContextAccessor();
        services.AddTransient<DirectoryForwardAuthorizationHandler>();

        services.AddHttpClient<DirectoryHttpRebacClient>(c => c.BaseAddress = baseUri)
            .AddHttpMessageHandler<DirectoryForwardAuthorizationHandler>();
        services.AddHttpClient<DirectoryHttpPolicyEvaluator>(c => c.BaseAddress = baseUri)
            .AddHttpMessageHandler<DirectoryForwardAuthorizationHandler>();

        services.AddScoped<IRebacClient>(sp => sp.GetRequiredService<DirectoryHttpRebacClient>());
        services.AddScoped<IPolicyEvaluator>(sp => sp.GetRequiredService<DirectoryHttpPolicyEvaluator>());
        return services;
    }
}
