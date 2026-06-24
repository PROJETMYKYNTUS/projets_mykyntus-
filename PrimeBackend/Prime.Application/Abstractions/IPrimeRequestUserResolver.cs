using Microsoft.AspNetCore.Http;
using Prime.Domain.Entities;

namespace Prime.Application.Abstractions;

/// <summary>
/// Résout l'utilisateur PRIME à partir des en-têtes <c>X-Prime-User-Id</c> / <c>X-Prime-Role</c>
/// ou des paramètres de requête / corps.
/// </summary>
public interface IPrimeRequestUserResolver
{
    const string HeaderUserId = "X-Prime-User-Id";
    const string HeaderRole = "X-Prime-Role";

    static string ExpandRole(string role)
    {
        var x = role.Trim();
        return x switch
        {
            "Comptable" => "Comptabilité",
            "Comptabilite" => "Comptabilité",
            "Coach" => "Référent technique",
            "ReferentTechnique" => "Référent technique",
            "Référent technique" => "Référent technique",
            "ChefDeProjet" => "Chef de projet",
            "Chef de projet" => "Chef de projet",
            "RP" => "Chef de projet",
            "Employee" => "Pilote",
            "Pilote" => "Pilote",
            "Superviseur" => "Superviseur",
            "Manager" => "Manager",
            _ => x,
        };
    }

    static bool RolesMatch(string roleA, string roleB) =>
        string.Equals(ExpandRole(roleA), ExpandRole(roleB), StringComparison.Ordinal);

    Task<PrimeResolvedUser?> TryResolveAsync(HttpRequest request, string? bodyUserId, string? bodyRole, CancellationToken ct = default);

    Task<PrimeResolvedUser?> TryResolveForValidationAsync(
        HttpRequest request,
        string? queryUserId,
        string? queryRole,
        CancellationToken ct = default);

    Task<PrimeResolvedUser?> TryResolveForValidationAsync(
        string? queryUserId,
        string? queryRole,
        CancellationToken ct = default);

    Task<PrimeResolvedUser?> TryResolveAsync(string? bodyUserId, string? bodyRole, CancellationToken ct = default);
}

public sealed record PrimeResolvedUser(string UserId, string Role, Employee Employee);
