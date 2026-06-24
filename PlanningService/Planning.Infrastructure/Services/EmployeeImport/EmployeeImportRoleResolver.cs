using Planning.Domain.Entities;

namespace Planning.Infrastructure.Services.EmployeeImport;

public sealed class RoleResolveResult
{
    public string? CanonicalRoleName { get; init; }
    public int? RoleId { get; init; }
    public string Confidence { get; init; } = "low";
    public string? ErrorMessage { get; init; }
    public bool IsForbidden => ErrorMessage is not null && CanonicalRoleName is null && RoleId is null;
}

public static class EmployeeImportRoleResolver
{
    public static RoleResolveResult Resolve(string? raw, IReadOnlyList<Role> roles)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new RoleResolveResult { ErrorMessage = "Rôle manquant." };

        var forbiddenMessage = EmployeeImportRoleSynonymRegistry.GetForbiddenRoleMessage(raw);
        if (forbiddenMessage is not null)
            return new RoleResolveResult { ErrorMessage = forbiddenMessage };

        if (int.TryParse(raw.Trim(), out var roleId))
        {
            var byId = roles.FirstOrDefault(r => r.Id == roleId);
            if (byId is not null)
            {
                forbiddenMessage = EmployeeImportRoleSynonymRegistry.GetForbiddenRoleMessage(byId.Name);
                if (forbiddenMessage is not null)
                    return new RoleResolveResult { ErrorMessage = forbiddenMessage };

                return new RoleResolveResult
                {
                    CanonicalRoleName = byId.Name,
                    RoleId = byId.Id,
                    Confidence = "high"
                };
            }
        }

        if (!EmployeeImportRoleSynonymRegistry.TryResolveCanonicalRole(raw, out var canonical))
            return new RoleResolveResult { ErrorMessage = $"Rôle invalide : « {raw} »." };

        forbiddenMessage = EmployeeImportRoleSynonymRegistry.GetForbiddenRoleMessage(canonical);
        if (forbiddenMessage is not null)
            return new RoleResolveResult { ErrorMessage = forbiddenMessage };

        var allowed = EmployeeImportRoleSynonymRegistry.ImportAllowedRoles(roles).ToList();
        var exact = allowed.FirstOrDefault(r =>
            string.Equals(r.Name, canonical, StringComparison.OrdinalIgnoreCase)
            || EmployeeImportColumnMatcher.Normalize(r.Name) == EmployeeImportColumnMatcher.Normalize(canonical));

        if (exact is not null)
        {
            return new RoleResolveResult
            {
                CanonicalRoleName = exact.Name,
                RoleId = exact.Id,
                Confidence = "high"
            };
        }

        var fuzzy = EmployeeImportFuzzyMatcher.FindBestMatch(
            canonical,
            allowed.Select(r => r.Name).ToList());

        if (fuzzy is null)
            return new RoleResolveResult { ErrorMessage = $"Rôle invalide ou non autorisé à l'import : « {raw} »." };

        var matchedRole = allowed.First(r => string.Equals(r.Name, fuzzy.Value, StringComparison.OrdinalIgnoreCase));
        return new RoleResolveResult
        {
            CanonicalRoleName = matchedRole.Name,
            RoleId = matchedRole.Id,
            Confidence = fuzzy.Confidence
        };
    }
}
