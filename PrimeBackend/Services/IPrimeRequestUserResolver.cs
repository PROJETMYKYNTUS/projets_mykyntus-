using Microsoft.AspNetCore.Http;
using PrimeBackend.Data;

namespace PrimeBackend.Services;

/// <summary>
/// Résout l'utilisateur PRIME à partir des en-têtes <c>X-Prime-User-Id</c> / <c>X-Prime-Role</c>
/// (prioritaires) ou des paramètres de requête / corps, puis vérifie la cohérence avec <see cref="EmployeeEntity"/>.
/// Extension JWT : remplacer l'implémentation par des claims issus du bearer token.
/// </summary>
public interface IPrimeRequestUserResolver
{
    const string HeaderUserId = "X-Prime-User-Id";
    const string HeaderRole = "X-Prime-Role";

    /// <summary>Normalise slug d’en-tête ASCII (ReferentTechnique) ou alias legacy vers le libellé métier.</summary>
    static string ExpandRole(string role)
    {
        var x = role.Trim();
        return x switch
        {
            "Comptable" => "Comptabilité",
            "Comptabilite" => "Comptabilité",
            "Coach" => PrimeFicheValidationRoles.ReferentTechnique,
            "ReferentTechnique" => PrimeFicheValidationRoles.ReferentTechnique,
            "Référent technique" => PrimeFicheValidationRoles.ReferentTechnique,
            "ChefDeProjet" => PrimeFicheValidationRoles.ChefDeProjet,
            "Chef de projet" => PrimeFicheValidationRoles.ChefDeProjet,
            "RP" => PrimeFicheValidationRoles.ChefDeProjet,
            "Employee" => "Pilote",
            "Pilote" => "Pilote",
            "Superviseur" => "Superviseur",
            "Manager" => "Manager",
            _ => x,
        };
    }

    /// <summary>Égalité rôles (Comptable / Comptabilité, Coach / Référent, slugs d’en-tête).</summary>
    static bool RolesMatch(string roleA, string roleB) =>
        string.Equals(ExpandRole(roleA), ExpandRole(roleB), StringComparison.Ordinal);

    Task<PrimeResolvedUser?> TryResolveAsync(HttpRequest request, string? bodyUserId, string? bodyRole, CancellationToken ct = default);

    /// <summary>
    /// Résolution pour les écrans de validation : employé par id, rôle d’action = rôle déclaré (UI / query),
    /// sans exiger la cohérence avec <see cref="EmployeeEntity.Role"/> en base.
    /// </summary>
    Task<PrimeResolvedUser?> TryResolveForValidationAsync(
        HttpRequest request,
        string? queryUserId,
        string? queryRole,
        CancellationToken ct = default);
}

public sealed record PrimeResolvedUser(string UserId, string Role, EmployeeEntity Employee);
