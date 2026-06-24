namespace Parrainage.Application.Abstractions;

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
            "RESPONSABLE PROJET" or "RESPONSABLE_PROJET" or "CHEF DE PROJET" or "CHEF_DE_PROJET" => "RP",
            "REFERENT TECHNIQUE" or "REFERENT_TECHNIQUE" or "RÉFÉRENT TECHNIQUE" => "COACH",
            "SUPERVISEUR" => "MANAGER",
            "PILOTE" or "EMPLOYEE" => "PILOTE",
            "ADMINISTRATEUR" => "ADMIN",
            "COMPTABILITE" or "COMPTABLE" => "COMPTA",
            _ => x,
        };
    }

    ParrainageResolvedUser Resolve(
        string? headerUserId,
        string? headerRole,
        string? headerProjectId,
        string? queryRole = null,
        string? queryUserId = null,
        string? queryProjectId = null);
}

public sealed record ParrainageResolvedUser(string UserId, string Role, string? ProjectId, bool IsDefault);
