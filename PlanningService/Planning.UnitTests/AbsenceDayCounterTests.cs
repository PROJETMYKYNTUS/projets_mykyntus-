using Planning.Infrastructure.Services;

namespace Planning.UnitTests;

public class AbsenceDayCounterTests
{
    [Fact]
    public void CountUnionMonToSatDays_ExcludesSunday_IncludesSaturday()
    {
        // Week spanning Fri May 1 2026 to Tue May 5 2026 — includes one Saturday, one Sunday
        var monthStart = new DateOnly(2026, 5, 1);
        var monthEnd = new DateOnly(2026, 5, 31);
        var ranges = new[] { (new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5)) };

        var count = PlanningAbsenceDayCounter.CountUnionMonToSatDays(ranges, monthStart, monthEnd);

        // Fri 1, Sat 2, Sun 3 (excl), Mon 4, Tue 5 => 4 days
        Assert.Equal(4, count);
    }

    [Fact]
    public void CountUnionMonToSatDays_DeduplicatesOverlappingRanges()
    {
        var monthStart = new DateOnly(2026, 6, 1);
        var monthEnd = new DateOnly(2026, 6, 30);
        var ranges = new[]
        {
            (new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 3)),
            (new DateOnly(2026, 6, 3), new DateOnly(2026, 6, 5)),
        };

        var count = PlanningAbsenceDayCounter.CountUnionMonToSatDays(ranges, monthStart, monthEnd);

        // Mon 1, Tue 2, Wed 3, Thu 4, Fri 5 => 5 (Wed not double-counted)
        Assert.Equal(5, count);
    }

    [Fact]
    public void CountUnionMonToSatDays_ClipsToMonthBoundary()
    {
        var monthStart = new DateOnly(2026, 7, 1);
        var monthEnd = new DateOnly(2026, 7, 31);
        var ranges = new[] { (new DateOnly(2026, 6, 28), new DateOnly(2026, 7, 2)) };

        var count = PlanningAbsenceDayCounter.CountUnionMonToSatDays(ranges, monthStart, monthEnd);

        // Only Jul 1 (Wed), Jul 2 (Thu) in month => 2
        Assert.Equal(2, count);
    }

    [Fact]
    public void TryParsePrimePeriod_ValidAndInvalid()
    {
        Assert.True(PlanningAbsenceDayCounter.TryParsePrimePeriod("2026-05", out var start, out var end));
        Assert.Equal(new DateOnly(2026, 5, 1), start);
        Assert.Equal(new DateOnly(2026, 5, 31), end);

        Assert.False(PlanningAbsenceDayCounter.TryParsePrimePeriod("invalid", out _, out _));
        Assert.False(PlanningAbsenceDayCounter.TryParsePrimePeriod("2026-13", out _, out _));
    }
}
