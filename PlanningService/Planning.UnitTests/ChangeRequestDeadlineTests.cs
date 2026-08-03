using Planning.Infrastructure.Services;

namespace Planning.UnitTests;

public class ChangeRequestDeadlineTests
{
    [Fact]
    public void EnsureCreationDeadline_allows_before_day_before_assignment()
    {
        // Jour concerné = mercredi 2026-07-22 → deadline mardi 2026-07-21 23:59 Casablanca
        var assignmentDate = new DateOnly(2026, 7, 22);
        var before = new DateTime(2026, 7, 21, 10, 0, 0, DateTimeKind.Utc);
        PlanningChangeRequestService.EnsureCreationDeadline(assignmentDate, before);
    }

    [Fact]
    public void EnsureCreationDeadline_blocks_after_day_before_assignment()
    {
        var assignmentDate = new DateOnly(2026, 7, 22);
        // Mercredi matin = après la veille 23:59
        var after = new DateTime(2026, 7, 22, 8, 0, 0, DateTimeKind.Utc);
        var ex = Assert.Throws<InvalidOperationException>(
            () => PlanningChangeRequestService.EnsureCreationDeadline(assignmentDate, after));
        Assert.Contains("Délai dépassé", ex.Message);
        Assert.Contains("veille", ex.Message);
    }
}
