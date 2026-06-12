namespace Kyntus.Messaging.Contracts;

/// <summary>
/// Helpers de compatibilité pendant et après le renommage des rôles (Phase 2A).
/// </summary>
public static class KyntusRoleNames
{
    public const string Pilote = "Pilote";
    public const string Employee = "Employee";
    public const string ReferentTechnique = "Référent technique";
    public const string Coach = "Coach";
    public const string ChefDeProjet = "Chef de projet";
    public const string Rp = "RP";
    public const string Superviseur = "Superviseur";
    public const string Manager = "Manager";

    public static bool IsSuperviseur(string? role) =>
        string.Equals(role, Manager, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, Superviseur, StringComparison.OrdinalIgnoreCase);

    public static bool IsPilote(string? role) =>
        string.Equals(role, Employee, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, Pilote, StringComparison.OrdinalIgnoreCase);

    public static bool IsReferentTechnique(string? role) =>
        string.Equals(role, Coach, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, ReferentTechnique, StringComparison.OrdinalIgnoreCase);

    public static bool IsChefDeProjet(string? role) =>
        string.Equals(role, Rp, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, ChefDeProjet, StringComparison.OrdinalIgnoreCase);

    public static string NormalizePlanningRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return Employee;
        if (IsPilote(role)) return Pilote;
        if (IsReferentTechnique(role)) return ReferentTechnique;
        if (IsChefDeProjet(role)) return ChefDeProjet;
        if (IsSuperviseur(role)) return Superviseur;
        return role;
    }
}
