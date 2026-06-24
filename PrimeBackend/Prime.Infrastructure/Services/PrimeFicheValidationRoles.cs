namespace Prime.Infrastructure.Services;



/// <summary>

/// Séparation métier : validation des <b>fiches</b> (rôles opérationnels) vs fichier <b>synthèse globale</b> (RH, Manager, Comptabilité).

/// </summary>

public static class PrimeFicheValidationRoles

{

    public const string Superviseur = "Superviseur";

    public const string ChefDeProjet = "Chef de projet";

    public const string ReferentTechnique = "Référent technique";



    private static readonly HashSet<string> OperationalApprovers = new(StringComparer.Ordinal)

    {

        ReferentTechnique,

        Superviseur,

        ChefDeProjet,

    };



    private static readonly HashSet<string> GlobalPoolOnly = new(StringComparer.Ordinal)

    {

        "RH",

        "Manager",

        "Comptabilité",

        "Comptable",

    };



    public static bool IsOperationalApprover(string role)

    {

        if (string.IsNullOrWhiteSpace(role)) return false;

        var r = role.Trim();

        if (OperationalApprovers.Contains(r)) return true;

        if (string.Equals(r, "RP", StringComparison.Ordinal)) return true;

        if (string.Equals(r, "Coach", StringComparison.Ordinal)) return true;

        return false;

    }



    public static bool IsGlobalPoolStakeholder(string role)

    {

        if (string.IsNullOrWhiteSpace(role)) return false;

        var r = role.Trim();

        if (GlobalPoolOnly.Contains(r)) return true;

        return IPrimeRequestUserResolver.RolesMatch(r, "Comptabilité");

    }

}


