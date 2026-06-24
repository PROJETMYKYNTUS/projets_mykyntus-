namespace Parrainage.Application.Authorization;

public static class ParrainageRoleGuard
{
    public static bool IsRh(string role) =>
        role is "RH" or "ADMIN";

    public static bool IsCompta(string role) =>
        role is "COMPTA" or "COMPTABILITE" or "ADMIN";

    public static bool CanMarkPayment(string role) => IsCompta(role);
}
