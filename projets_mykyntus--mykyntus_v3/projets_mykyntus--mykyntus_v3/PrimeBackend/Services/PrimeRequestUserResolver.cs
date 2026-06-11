using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;

namespace PrimeBackend.Services;

public sealed class PrimeRequestUserResolver(PrimeDbContext db) : IPrimeRequestUserResolver
{
    public async Task<PrimeResolvedUser?> TryResolveAsync(HttpRequest request, string? bodyUserId, string? bodyRole, CancellationToken ct = default)
    {
        var userId = FirstNonEmpty(request.Headers[IPrimeRequestUserResolver.HeaderUserId].FirstOrDefault(), bodyUserId);
        var roleRaw = FirstNonEmpty(request.Headers[IPrimeRequestUserResolver.HeaderRole].FirstOrDefault(), bodyRole);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(roleRaw))
            return null;

        var role = IPrimeRequestUserResolver.ExpandRole(roleRaw);
        var emp = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == userId.Trim(), ct);
        if (emp is null)
            return null;

        if (!IPrimeRequestUserResolver.RolesMatch(emp.Role, role))
            return null;

        return new PrimeResolvedUser(emp.Id, emp.Role.Trim(), emp);
    }

    public async Task<PrimeResolvedUser?> TryResolveForValidationAsync(
        HttpRequest request,
        string? queryUserId,
        string? queryRole,
        CancellationToken ct = default)
    {
        var userId = FirstNonEmpty(
            request.Headers[IPrimeRequestUserResolver.HeaderUserId].FirstOrDefault(),
            queryUserId);
        var roleRaw = FirstNonEmpty(
            request.Headers[IPrimeRequestUserResolver.HeaderRole].FirstOrDefault(),
            queryRole);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(roleRaw))
            return null;

        var actingRole = IPrimeRequestUserResolver.ExpandRole(roleRaw);
        var emp = await db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == userId.Trim(), ct);
        if (emp is null)
            return null;

        return new PrimeResolvedUser(emp.Id, actingRole, emp);
    }

    private static string? FirstNonEmpty(string? a, string? b)
    {
        if (!string.IsNullOrWhiteSpace(a)) return a.Trim();
        if (!string.IsNullOrWhiteSpace(b)) return b.Trim();
        return null;
    }
}
