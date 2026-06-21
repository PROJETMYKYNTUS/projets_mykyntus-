using Kyntus.Identity.Jwt;
using Kyntus.Messaging.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ParrainageBackend.Data;

namespace ParrainageBackend.Services;

public sealed class ParrainageRequestUserResolver(
    IHttpContextAccessor httpContextAccessor,
    IServiceProvider serviceProvider,
    IHostEnvironment hostEnvironment,
    ILogger<ParrainageRequestUserResolver> logger) : IParrainageRequestUserResolver
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
        var fromJwt = TryResolveFromJwt(queryProjectId);
        var fromHeaders = TryResolveFromHeaders(request, queryRole, queryUserId, queryProjectId);

        if (fromJwt is not null || fromHeaders is not null)
        {
            var role = ResolveEffectiveRole(fromJwt?.Role, fromHeaders?.Role);
            var userId = FirstNonEmpty(
                fromJwt is { IsDefault: false } ? fromJwt.UserId : null,
                fromHeaders?.UserId,
                fromJwt?.UserId);
            var projectId = FirstNonEmpty(fromHeaders?.ProjectId, fromJwt?.ProjectId, queryProjectId);
            var isDefault = fromJwt?.IsDefault != false && fromHeaders is null;

            if (fromJwt is not null && fromHeaders is not null && role != fromJwt.Role)
            {
                logger.LogDebug(
                    "PARRAINAGE : rôle effectif {Role} (JWT={JwtRole}, en-tête={HeaderRole}).",
                    role,
                    fromJwt.Role,
                    fromHeaders.Role);
            }

            return new ParrainageResolvedUser(
                userId ?? (hostEnvironment.IsDevelopment() ? "emp-1" : "unknown"),
                role,
                projectId,
                IsDefault: isDefault);
        }

        if (hostEnvironment.IsDevelopment())
        {
            logger.LogDebug("PARRAINAGE : en-têtes identité absents — défaut PILOTE/emp-1 (mode dev).");
            return new ParrainageResolvedUser("emp-1", "PILOTE", queryProjectId, IsDefault: true);
        }

        logger.LogWarning("PARRAINAGE : identité non résolue (JWT et en-têtes absents).");
        return new ParrainageResolvedUser("unknown", "PILOTE", queryProjectId, IsDefault: true);
    }

    private ParrainageResolvedUser? TryResolveFromHeaders(
        HttpRequest request,
        string? queryRole,
        string? queryUserId,
        string? queryProjectId)
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
            return null;

        var role = IParrainageRequestUserResolver.NormalizeRole(roleRaw);
        if (!AllowedRoles.Contains(role))
        {
            logger.LogWarning("PARRAINAGE : rôle en-tête inconnu « {Role} » — défaut PILOTE.", roleRaw);
            role = "PILOTE";
        }

        return new ParrainageResolvedUser(userId.Trim(), role, projectId, IsDefault: false);
    }

    private ParrainageResolvedUser? TryResolveFromJwt(string? queryProjectId)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            return null;

        var email = principal.GetEmail();
        if (string.IsNullOrWhiteSpace(email))
            return ResolveFromJwtClaims(principal, queryProjectId);

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetService<ParrainageDbContext>();
        if (db is null)
            return ResolveFromJwtClaims(principal, queryProjectId);

        var needle = email.Trim().ToLowerInvariant();
        var row = db.PortalUsers.AsNoTracking()
            .FirstOrDefault(u => u.Email.ToLower() == needle);
        if (row is null)
        {
            logger.LogWarning(
                "PARRAINAGE : aucun utilisateur portail pour {Email} — repli sur les claims JWT.",
                email);
            return ResolveFromJwtClaims(principal, queryProjectId);
        }

        var portalRole = IParrainageRequestUserResolver.NormalizeRole(row.Role);
        var jwtRole = ResolveJwtParrainageRole(principal);
        var role = ResolveEffectiveRole(jwtRole, portalRole);
        var projectId = queryProjectId ?? row.ProjectId;
        return new ParrainageResolvedUser(row.Id, role, projectId, IsDefault: false);
    }

    private static ParrainageResolvedUser? ResolveFromJwtClaims(
        System.Security.Claims.ClaimsPrincipal principal,
        string? queryProjectId)
    {
        var jwtRole = ResolveJwtParrainageRole(principal);
        if (string.IsNullOrWhiteSpace(jwtRole))
            return null;

        var userId = FirstNonEmpty(
            principal.GetSubjectId()?.ToString(),
            principal.GetAuthUserId()?.ToString());
        if (string.IsNullOrWhiteSpace(userId))
            userId = "unknown";

        return new ParrainageResolvedUser(userId, jwtRole, queryProjectId, IsDefault: true);
    }

    /// <summary>
    /// Le rôle privilégié (JWT Auth RH/Admin/Compta…) prime sur le rôle portail ou en-tête SPA.
    /// Si le rôle JWT/portail est PILOTE par défaut, l'en-tête SPA (rôle métier) est préféré.
    /// </summary>
    internal static string ResolveEffectiveRole(string? primaryRole, string? secondaryRole)
    {
        if (!string.IsNullOrWhiteSpace(primaryRole) && IsPrivilegedParrainageRole(primaryRole))
            return primaryRole;
        if (!string.IsNullOrWhiteSpace(secondaryRole) && IsPrivilegedParrainageRole(secondaryRole))
            return secondaryRole;
        if (string.Equals(primaryRole, "PILOTE", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(secondaryRole)
            && !string.Equals(secondaryRole, "PILOTE", StringComparison.Ordinal))
            return secondaryRole;
        if (!string.IsNullOrWhiteSpace(primaryRole))
            return primaryRole;
        if (!string.IsNullOrWhiteSpace(secondaryRole))
            return secondaryRole;
        return "PILOTE";
    }

    private static string? ResolveJwtParrainageRole(System.Security.Claims.ClaimsPrincipal principal)
    {
        var raw = principal.GetAuthRole();
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        return IParrainageRequestUserResolver.NormalizeRole(KyntusPortalRoleMapping.ToParrainageRole(raw));
    }

    private static bool IsPrivilegedParrainageRole(string role) =>
        role is "RH" or "ADMIN" or "COMPTA" or "COMPTABILITE" or "AUDIT";

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }
}
