using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Services;

public sealed class PrimeAbsenceSanctionService(
    IPlanningAbsenceClient planningAbsence,
    IPrimeAbsenceSanctionConfigService config)
{
    public Task<int> GetDivisorDaysAsync(CancellationToken ct = default) =>
        config.GetDivisorDaysAsync(ct);

    public async Task ApplyAbsenceSanctionAsync(
        GlobalPoolSynthesisLineEntity line,
        string period,
        CancellationToken ct = default)
    {
        var counts = await planningAbsence.GetAbsenceDayCountsAsync(period, [line.EmployeeId], ct);
        counts.TryGetValue(line.EmployeeId, out var absenceDays);

        var divisor = await config.GetDivisorDaysAsync(ct);
        var totalInitial = line.TotalAmount ?? 0m;
        var sanction = PrimeAbsenceSanctionCalculator.ComputeSanction(totalInitial, absenceDays, divisor);
        var net = PrimeAbsenceSanctionCalculator.ComputeNetPayable(
            totalInitial,
            sanction,
            line.RegularizationAmount);

        line.AbsenceDayCount = absenceDays;
        line.SanctionAmount = sanction;
        line.NetPayableAmount = net;
        line.AbsenceComputedAt = DateTimeOffset.UtcNow;
    }

    public async Task<Dictionary<string, int>> FetchAbsenceCountsAsync(
        string period,
        IEnumerable<string> employeeIds,
        CancellationToken ct = default)
    {
        var ids = employeeIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var counts = await planningAbsence.GetAbsenceDayCountsAsync(period, ids, ct);
        return new Dictionary<string, int>(counts, StringComparer.OrdinalIgnoreCase);
    }

    public void RecalculateFromStoredAbsences(GlobalPoolSynthesisLineEntity line, int divisorDays)
    {
        var totalInitial = line.TotalAmount ?? 0m;
        line.SanctionAmount = PrimeAbsenceSanctionCalculator.ComputeSanction(
            totalInitial,
            line.AbsenceDayCount,
            divisorDays);
        line.NetPayableAmount = PrimeAbsenceSanctionCalculator.ComputeNetPayable(
            totalInitial,
            line.SanctionAmount,
            line.RegularizationAmount);
    }
}
