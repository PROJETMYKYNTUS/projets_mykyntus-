using Planning.Infrastructure.Services;

namespace Planning.UnitTests;

public class ChangeRequestDeadlineTests
{
    [Fact]
    public void EnsureCreationDeadline_allows_before_wednesday_prev_week()
    {
        // Planning week starts Monday 2026-07-20 → prev week Wed deadline = 2026-07-15 23:59
        var weekStart = new DateOnly(2026, 7, 20);
        var before = new DateTime(2026, 7, 15, 20, 0, 0, DateTimeKind.Utc);
        // Convert: Casablanca is UTC+1 typically — use local-ish by calling with utc that is clearly before
        PlanningChangeRequestService.EnsureCreationDeadline(weekStart, before);
    }

    [Fact]
    public void EnsureCreationDeadline_blocks_after_wednesday_prev_week()
    {
        var weekStart = new DateOnly(2026, 7, 20);
        // Thursday after deadline Wednesday (UTC far after)
        var after = new DateTime(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc);
        var ex = Assert.Throws<InvalidOperationException>(
            () => PlanningChangeRequestService.EnsureCreationDeadline(weekStart, after));
        Assert.Contains("Délai dépassé", ex.Message);
    }
}
