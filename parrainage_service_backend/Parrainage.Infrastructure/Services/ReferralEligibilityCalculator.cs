using Parrainage.Infrastructure.Persistence;

namespace Parrainage.Infrastructure.Services;

public static class ReferralEligibilityCalculator
{
    public static DateOnly ResolveCountingStartDate(ReferralEntity r) =>
        r.ProductionStartDate ?? r.CandidateStartDate!.Value;

    public static DateTimeOffset ComputeEligibleForPayment(DateOnly countingStartDate, int minDurationMonths)
    {
        var utcStart = countingStartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return new DateTimeOffset(utcStart.AddMonths(minDurationMonths), TimeSpan.Zero);
    }
}
