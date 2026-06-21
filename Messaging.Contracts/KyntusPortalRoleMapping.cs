namespace Kyntus.Messaging.Contracts;

/// <summary>Mapping rôles Planning/JWT vers modules miroirs (Documentation, Parrainage).</summary>
public static class KyntusPortalRoleMapping
{
    public static string ToParrainageRole(string? planningRole)
    {
        if (KyntusRoleNames.IsSuperviseur(planningRole)) return "MANAGER";
        if (KyntusRoleNames.IsReferentTechnique(planningRole)) return "COACH";
        if (KyntusRoleNames.IsChefDeProjet(planningRole)) return "RP";
        if (KyntusRoleNames.IsPilote(planningRole)) return "PILOTE";
        var r = planningRole?.Trim().ToUpperInvariant() ?? "PILOTE";
        return r switch
        {
            "RH" or "EQUIPE FORMATION" or "EQUIPE_FORMATION" => "RH",
            "ADMIN" => "ADMIN",
            "AUDIT" => "AUDIT",
            "COMPTABILITE" or "COMPTA" => "COMPTA",
            _ => "PILOTE",
        };
    }

    /// <summary>Noms PostgreSQL enum documentation.app_role (pilote, coach, manager, rp, rh, admin, audit).</summary>
    public static string ToDocumentationRoleName(string? planningRole)
    {
        if (KyntusRoleNames.IsPilote(planningRole)) return "pilote";
        if (KyntusRoleNames.IsReferentTechnique(planningRole)) return "coach";
        if (KyntusRoleNames.IsSuperviseur(planningRole)) return "manager";
        if (KyntusRoleNames.IsChefDeProjet(planningRole)) return "rp";
        var r = planningRole?.Trim().ToLowerInvariant() ?? "pilote";
        return r switch
        {
            "rh" or "equipe formation" or "equipe_formation" => "rh",
            "admin" => "admin",
            "audit" => "audit",
            _ => "pilote",
        };
    }
}
