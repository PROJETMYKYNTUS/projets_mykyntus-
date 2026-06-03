namespace ParrainageBackend.Services;

public static class ParrainageRoleGuard
{
    public static bool IsRh(string role) =>
        role is "RH" or "ADMIN";

    public static bool IsCompta(string role) =>
        role is "COMPTA" or "COMPTABILITE" or "ADMIN";

    public static bool CanMarkPayment(string role) => IsCompta(role);
}

public static class ReferralEligibilityCalculator
{
    public static DateTimeOffset ComputeEligibleForPayment(DateOnly candidateStartDate, int minDurationMonths)
    {
        var utcStart = candidateStartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return new DateTimeOffset(utcStart.AddMonths(minDurationMonths), TimeSpan.Zero);
    }
}
