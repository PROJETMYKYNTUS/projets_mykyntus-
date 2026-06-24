namespace Prime.Application.Allowance;

public static class AllowanceValidationRoles
{
    public const string Manager = "Manager";
    public const string Rh = "RH";
    public const string Comptabilite = "Comptabilité";
    public const string Comptable = "Comptable";
    public const string Admin = "Admin";

    public static bool IsAllowanceStakeholder(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        var r = role.Trim();
        return r.Equals(Manager, StringComparison.OrdinalIgnoreCase)
               || r.Equals(Rh, StringComparison.OrdinalIgnoreCase)
               || r.Equals(Comptabilite, StringComparison.OrdinalIgnoreCase)
               || r.Equals(Comptable, StringComparison.OrdinalIgnoreCase)
               || r.Equals(Admin, StringComparison.OrdinalIgnoreCase);
    }

    public static string? ExpectedRoleForStatus(string status) => status switch
    {
        AllowanceRequestStatuses.ManagerApproved => Rh,
        AllowanceRequestStatuses.RhApproved => Comptabilite,
        AllowanceRequestStatuses.ComptaApproved => Comptabilite,
        _ => null,
    };

    public static bool CanActAtStatus(string actorRole, string status)
    {
        if (string.IsNullOrWhiteSpace(actorRole)) return false;
        var r = actorRole.Trim();
        return status switch
        {
            AllowanceRequestStatuses.ManagerApproved => r.Equals(Rh, StringComparison.OrdinalIgnoreCase),
            AllowanceRequestStatuses.RhApproved => r.Equals(Comptabilite, StringComparison.OrdinalIgnoreCase)
                                                   || r.Equals(Comptable, StringComparison.OrdinalIgnoreCase),
            AllowanceRequestStatuses.ComptaApproved => r.Equals(Comptabilite, StringComparison.OrdinalIgnoreCase)
                                                       || r.Equals(Comptable, StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    public static string NextStatusAfterApproval(string currentStatus) => currentStatus switch
    {
        AllowanceRequestStatuses.Submitted => AllowanceRequestStatuses.ManagerApproved,
        AllowanceRequestStatuses.ManagerApproved => AllowanceRequestStatuses.RhApproved,
        AllowanceRequestStatuses.RhApproved => AllowanceRequestStatuses.ComptaApproved,
        AllowanceRequestStatuses.ComptaApproved => AllowanceRequestStatuses.Paid,
        _ => currentStatus,
    };
}
