using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kyntus.Iam;

public sealed class PolicyEvaluator(
    IPermissionCatalog catalog,
    IRebacClient rebac,
    ILogger<PolicyEvaluator> logger) : IPolicyEvaluator
{
    public async Task<PolicyDecision> EvaluateAsync(PolicyRequest request, CancellationToken ct = default)
    {
        var role = request.Role.Trim();
        var action = request.Action.Trim();

        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "RH", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Audit", StringComparison.OrdinalIgnoreCase))
        {
            return new PolicyDecision(true);
        }

        var scopes = new[] { "Global", "Self", "Subtree", "Pole", "Cellule", "Service" };
        foreach (var scope in scopes)
        {
            if (await catalog.RoleHasActionAsync(role, action, scope, ct))
            {
                if (scope == "Global") return new PolicyDecision(true);
                if (scope == "Self" && request.ResourceId is not null
                    && Guid.TryParse(request.ResourceId, out var rid)
                    && rid == request.SubjectId)
                    return new PolicyDecision(true);

                if (scope == "Subtree" && request.ResourceId is not null
                    && Guid.TryParse(request.ResourceId, out var targetId)
                    && await rebac.IsDescendantAsync(request.SubjectId, targetId, ct))
                    return new PolicyDecision(true);

                if (scope is "Pole" or "Cellule" or "Service")
                {
                    var kind = scope switch
                    {
                        "Pole" => "ChefDeProjet",
                        "Cellule" => "Superviseur",
                        _ => "ReferentTechnique",
                    };
                    if (request.ResourceId is not null)
                    {
                        var nodes = await rebac.GetManagedNodeIdsAsync(request.SubjectId, kind, ct);
                        if (nodes.Contains(request.ResourceId, StringComparer.OrdinalIgnoreCase))
                            return new PolicyDecision(true);
                    }
                }
            }
        }

        logger.LogDebug("Policy denied {Role} {Action} on {ResourceType}/{ResourceId}", role, action, request.ResourceType, request.ResourceId);
        return new PolicyDecision(false, "Permission denied");
    }
}

public static class IamServiceCollectionExtensions
{
    public static IServiceCollection AddKyntusIam(this IServiceCollection services)
    {
        services.AddScoped<IPolicyEvaluator, PolicyEvaluator>();
        return services;
    }
}
