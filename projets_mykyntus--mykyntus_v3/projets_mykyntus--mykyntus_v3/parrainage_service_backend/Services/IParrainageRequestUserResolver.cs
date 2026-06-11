using Microsoft.AspNetCore.Http;

namespace ParrainageBackend.Services;

/// <summary>
/// Résout l'utilisateur démo à partir des en-têtes X-Parrainage-* (mode dev, sans JWT).
/// Extension JWT : remplacer par des claims issus du bearer token.
/// </summary>
public interface IParrainageRequestUserResolver
{
    const string HeaderUserId = "X-Parrainage-User-Id";
    const string HeaderRole = "X-Parrainage-Role";
    const string HeaderProjectId = "X-Parrainage-Project-Id";

    static string NormalizeRole(string role)
    {
        var x = role.Trim().ToUpperInvariant();
        return x switch
        {
            "RESPONSABLE PROJET" or "RESPONSABLE_PROJET" => "RP",
            "ADMINISTRATEUR" => "ADMIN",
            "COMPTABILITE" or "COMPTABLE" => "COMPTA",
            _ => x,
        };
    }

    ParrainageResolvedUser Resolve(HttpRequest request, string? queryRole = null, string? queryUserId = null, string? queryProjectId = null);
}

public sealed record ParrainageResolvedUser(string UserId, string Role, string? ProjectId, bool IsDefault);
