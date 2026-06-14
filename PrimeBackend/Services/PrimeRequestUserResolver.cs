using Kyntus.Identity.Jwt;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;

namespace PrimeBackend.Services;

public sealed class PrimeRequestUserResolver(
    PrimeDbContext db,
    IHttpContextAccessor httpContextAccessor,
    IHostEnvironment hostEnvironment) : IPrimeRequestUserResolver
{
    public async Task<PrimeResolvedUser?> TryResolveAsync(HttpRequest request, string? bodyUserId, string? bodyRole, CancellationToken ct = default)
    {
        var fromJwt = await TryResolveFromJwtAsync(ct);
        if (fromJwt is not null)
            return fromJwt;

        if (!hostEnvironment.IsDevelopment())
            return null;

        return await TryResolveFromExplicitIdentityAsync(request, bodyUserId, bodyRole, requireRoleMatch: true, ct);
    }

    public async Task<PrimeResolvedUser?> TryResolveForValidationAsync(
        HttpRequest request,
        string? queryUserId,
        string? queryRole,
        CancellationToken ct = default)
    {
        var fromJwt = await TryResolveFromJwtForValidationAsync(queryUserId, queryRole, ct);
        if (fromJwt is not null)
            return fromJwt;

        var isAuthenticated = httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
        if (!hostEnvironment.IsDevelopment() && !isAuthenticated)
            return null;

        return await TryResolveFromExplicitIdentityAsync(
            request,
            queryUserId,
            queryRole,
            requireRoleMatch: false,
            ct,
            requireJwtEmailMatch: isAuthenticated);
    }

    private async Task<PrimeResolvedUser?> TryResolveFromExplicitIdentityAsync(
        HttpRequest request,
        string? bodyUserId,
        string? bodyRole,
        bool requireRoleMatch,
        CancellationToken ct,
        bool requireJwtEmailMatch = false)
    {
        var userId = FirstNonEmpty(request.Headers[IPrimeRequestUserResolver.HeaderUserId].FirstOrDefault(), bodyUserId);
        var roleRaw = FirstNonEmpty(request.Headers[IPrimeRequestUserResolver.HeaderRole].FirstOrDefault(), bodyRole);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(roleRaw))
            return null;

        var role = IPrimeRequestUserResolver.ExpandRole(roleRaw);
        var emp = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == userId.Trim(), ct);
        if (emp is null)
            return null;

        if (requireJwtEmailMatch && !EmployeeMatchesJwtEmail(emp))
            return null;

        if (requireRoleMatch && !IPrimeRequestUserResolver.RolesMatch(emp.Role, role))
            return null;

        return new PrimeResolvedUser(emp.Id, role, emp);
    }

    private async Task<PrimeResolvedUser?> TryResolveFromJwtForValidationAsync(
        string? preferredUserId,
        string? preferredRole,
        CancellationToken ct)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            return null;

        var email = principal.GetEmail();
        EmployeeEntity? emp = null;

        if (!string.IsNullOrWhiteSpace(email))
        {
            var needle = email.Trim().ToLowerInvariant();
            emp = await db.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Email.ToLower() == needle, ct);
        }

        if (emp is null && !string.IsNullOrWhiteSpace(preferredUserId))
        {
            emp = await db.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == preferredUserId.Trim(), ct);
            if (emp is not null && !EmployeeMatchesJwtEmail(emp))
                emp = null;
        }

        if (emp is null)
            return null;

        var roleRaw = !string.IsNullOrWhiteSpace(preferredRole)
            ? preferredRole
            : principal.GetAuthRole();
        var role = string.IsNullOrWhiteSpace(roleRaw)
            ? emp.Role.Trim()
            : MapAuthRoleToPrimeRole(roleRaw);

        return new PrimeResolvedUser(emp.Id, IPrimeRequestUserResolver.ExpandRole(role), emp);
    }

    private async Task<PrimeResolvedUser?> TryResolveFromJwtAsync(CancellationToken ct)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            return null;

        var email = principal.GetEmail();
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var needle = email.Trim().ToLowerInvariant();
        var emp = await db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Email.ToLower() == needle, ct);
        if (emp is null)
            return null;

        var roleRaw = principal.GetAuthRole();
        var role = string.IsNullOrWhiteSpace(roleRaw)
            ? emp.Role.Trim()
            : MapAuthRoleToPrimeRole(roleRaw);

        return new PrimeResolvedUser(emp.Id, IPrimeRequestUserResolver.ExpandRole(role), emp);
    }

    private bool EmployeeMatchesJwtEmail(EmployeeEntity emp)
    {
        var email = httpContextAccessor.HttpContext?.User.GetEmail();
        if (string.IsNullOrWhiteSpace(email))
            return false;
        return string.Equals(emp.Email.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string MapAuthRoleToPrimeRole(string authRole)
    {
        var r = authRole.Trim().ToLowerInvariant();
        return r switch
        {
            "admin" => "Admin",
            "rh" => "RH",
            "manager" => "Manager",
            "coach" => PrimeFicheValidationRoles.ReferentTechnique,
            "rp" => PrimeFicheValidationRoles.ChefDeProjet,
            "audit" => "Audit",
            "employee" => "Pilote",
            "pilote" => "Pilote",
            "superviseur" => "Superviseur",
            "referent technique" or "referent_technique" => PrimeFicheValidationRoles.ReferentTechnique,
            "chef de projet" or "chef_de_projet" => PrimeFicheValidationRoles.ChefDeProjet,
            "equipe formation" or "equipe_formation" => "RH",
            _ => authRole.Trim(),
        };
    }

    private static string? FirstNonEmpty(string? a, string? b)
    {
        if (!string.IsNullOrWhiteSpace(a)) return a.Trim();
        if (!string.IsNullOrWhiteSpace(b)) return b.Trim();
        return null;
    }
}
