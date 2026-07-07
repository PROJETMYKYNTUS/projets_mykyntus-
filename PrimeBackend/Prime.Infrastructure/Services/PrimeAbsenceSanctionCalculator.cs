namespace Prime.Infrastructure.Services;

public static class PrimeAbsenceSanctionCalculator
{
    public const int DefaultDivisorDays = 26;

    public static decimal ComputeSanction(decimal totalInitial, int absenceDays, int divisorDays)
    {
        if (divisorDays <= 0 || absenceDays <= 0 || totalInitial <= 0)
            return 0m;

        return Math.Round(
            totalInitial * absenceDays / divisorDays,
            2,
            MidpointRounding.AwayFromZero);
    }

    public static decimal ComputeNetPayable(decimal totalInitial, decimal sanction, decimal regularization) =>
        totalInitial - sanction + regularization;
}
