using Kyntus.Identity.Jwt;
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
        if (fromJwt is not null)
            return fromJwt;

        if (!hostEnvironment.IsDevelopment())
        {
            logger.LogWarning("PARRAINAGE : JWT requis en production (identité non résolue).");
            return new ParrainageResolvedUser("unknown", "PILOTE", queryProjectId, IsDefault: true);
        }

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
            logger.LogDebug("PARRAINAGE : en-têtes identité absents — défaut PILOTE/emp-1 (mode dev).");
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

        var role = IParrainageRequestUserResolver.NormalizeRole(row.Role);
        var projectId = queryProjectId ?? row.ProjectId;
        return new ParrainageResolvedUser(row.Id, role, projectId, IsDefault: false);
    }

    private static ParrainageResolvedUser? ResolveFromJwtClaims(
        System.Security.Claims.ClaimsPrincipal principal,
        string? queryProjectId)
    {
        var userId = principal.GetSubjectId()?.ToString();
        var roleRaw = principal.GetAuthRole();
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(roleRaw))
            return null;

        var role = IParrainageRequestUserResolver.NormalizeRole(roleRaw);
        if (!AllowedRoles.Contains(role))
            role = "PILOTE";

        return new ParrainageResolvedUser(userId, role, queryProjectId, IsDefault: true);
    }

    private static string? FirstNonEmpty(string? a, string? b)
    {
        if (!string.IsNullOrWhiteSpace(a)) return a.Trim();
        if (!string.IsNullOrWhiteSpace(b)) return b.Trim();
        return null;
    }
}
