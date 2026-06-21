using PrimeBackend.Data;

namespace PrimeBackend.Services;

/// <summary>
/// Résolution du rôle acteur synthèse globale. Les managers opérationnels sont stockés « Superviseur »
/// côté Prime (sync Directory) mais agissent en « Manager » via JWT / rôle déclaré.
/// </summary>
public static class PrimeGlobalPoolActorResolver
{
    public static bool IsPoolStakeholderRole(string? role) =>
        role is "Manager" or "RH" or "Comptable" or "Comptabilité" or "Admin";

    public static bool IsOperationalDepartmentManager(EmployeeEntity emp) =>
        string.Equals(emp.BusinessDepartmentKind, "Operational", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Rôle effectif pour la synthèse globale, ou null si non autorisé.
    /// </summary>
    public static string? ResolveActingRole(
        EmployeeEntity emp,
        string realRole,
        string declaredRole,
        bool managesOperationalDepartment = false)
    {
        if (IsPoolStakeholderRole(realRole))
        {
            if (string.Equals(realRole, "Admin", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(declaredRole))
                return declaredRole.Trim();
            return realRole;
        }

        if (string.Equals(declaredRole, "Manager", StringComparison.Ordinal)
            && (IsOperationalDepartmentManager(emp) || managesOperationalDepartment))
            return "Manager";

        return null;
    }
}
