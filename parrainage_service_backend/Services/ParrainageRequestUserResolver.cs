using Microsoft.AspNetCore.Http;

namespace ParrainageBackend.Services;

public sealed class ParrainageRequestUserResolver(ILogger<ParrainageRequestUserResolver> logger) : IParrainageRequestUserResolver
{
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.Ordinal)
    {
        "PILOTE", "RH", "ADMIN", "MANAGER", "COACH", "RP", "AUDIT", "COMPTA", "COMPTABILITE",
    };

    public ParrainageResolvedUser Resolve(
        HttpRequest request,
        string? queryRole = null,
        string? queryUserId = null,
        string? queryProjectId = null)
    {
        var userId = FirstNonEmpty(
            request.Headers[IParrainageRequestUserResolver.HeaderUserId].FirstOrDefault(),
            queryUserId);
        var roleRaw = FirstNonEmpty(
            request.Headers[IParrainageRequestUserResolver.HeaderRole].FirstOrDefault(),
            queryRole);
        var projectId = FirstNonEmpty(
            request.Headers[IParrainageRequestUserResolver.HeaderProjectId].FirstOrDefault(),
            queryProjectId);

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(roleRaw))
        {
            logger.LogDebug(
                "PARRAINAGE : en-têtes identité absents — défaut PILOTE/emp-1 (mode dev).");
            return new ParrainageResolvedUser("emp-1", "PILOTE", projectId, IsDefault: true);
        }

        var role = IParrainageRequestUserResolver.NormalizeRole(roleRaw);
        if (!AllowedRoles.Contains(role))
        {
            logger.LogWarning("PARRAINAGE : rôle inconnu « {Role} » — défaut PILOTE.", roleRaw);
            role = "PILOTE";
        }

        return new ParrainageResolvedUser(userId.Trim(), role, projectId, IsDefault: false);
    }

    private static string? FirstNonEmpty(string? a, string? b)
    {
        if (!string.IsNullOrWhiteSpace(a)) return a.Trim();
        if (!string.IsNullOrWhiteSpace(b)) return b.Trim();
        return null;
    }
}
