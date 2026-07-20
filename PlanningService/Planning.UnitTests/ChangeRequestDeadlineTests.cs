using Planning.Infrastructure.Services;

namespace Planning.UnitTests;

public class ChangeRequestDeadlineTests
{
    [Fact]
    public void EnsureCreationDeadline_allows_before_wednesday_of_planning_week()
    {
        // Planning week starts Monday 2026-07-20 → deadline Wed 2026-07-22 23:59 Casablanca
        var weekStart = new DateOnly(2026, 7, 20);
        // Monday morning UTC of the planning week
        var before = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);
        PlanningChangeRequestService.EnsureCreationDeadline(weekStart, before);
    }

    [Fact]
    public void EnsureCreationDeadline_blocks_after_wednesday_of_planning_week()
    {
        var weekStart = new DateOnly(2026, 7, 20);
        // Thursday after Wednesday deadline
        var after = new DateTime(2026, 7, 23, 12, 0, 0, DateTimeKind.Utc);
        var ex = Assert.Throws<InvalidOperationException>(
            () => PlanningChangeRequestService.EnsureCreationDeadline(weekStart, after));
        Assert.Contains("Délai dépassé", ex.Message);
    }
}
