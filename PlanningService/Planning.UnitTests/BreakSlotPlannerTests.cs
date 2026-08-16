using Planning.Infrastructure.Services;

namespace Planning.UnitTests;

public class BreakSlotPlannerTests
{
    [Fact]
    public void WindowEnd_critical_is_plus_5h_max()
    {
        var start8 = new TimeOnly(8, 0);
        var start9 = new TimeOnly(9, 0);
        var start10 = new TimeOnly(10, 0);
        Assert.Equal(new TimeOnly(13, 0), BreakSlotPlanner.WindowEnd(start8, isCriticalCell: true));
        Assert.Equal(new TimeOnly(14, 0), BreakSlotPlanner.WindowEnd(start9, isCriticalCell: true));
        Assert.Equal(new TimeOnly(15, 0), BreakSlotPlanner.WindowEnd(start10, isCriticalCell: true));
        Assert.Equal(new TimeOnly(12, 30), BreakSlotPlanner.WindowEnd(start8, isCriticalCell: false));
        Assert.DoesNotContain(new TimeOnly(16, 0), BreakSlotPlanner.AllowedStarts(start9, true));
    }

    [Fact]
    public void IsOpeningShift_only_8h_and_9h()
    {
        Assert.True(BreakSlotPlanner.IsOpeningShift(new TimeOnly(8, 0)));
        Assert.True(BreakSlotPlanner.IsOpeningShift(new TimeOnly(9, 0)));
        Assert.False(BreakSlotPlanner.IsOpeningShift(new TimeOnly(10, 0)));
    }

    [Fact]
    public void AllowedStarts_critical_stops_at_plus_5h()
    {
        var start = new TimeOnly(8, 0);
        var critical = BreakSlotPlanner.AllowedStarts(start, isCriticalCell: true);
        Assert.Contains(new TimeOnly(13, 0), critical);
        Assert.DoesNotContain(new TimeOnly(13, 30), critical);
        Assert.DoesNotContain(new TimeOnly(14, 0), critical);
    }

    [Fact]
    public void ExtremeTier_no_plus_5h30()
    {
        var start = new TimeOnly(8, 0);
        var critical = BreakSlotPlanner.ExtremeTier(start, isCriticalCell: true);
        Assert.Contains(new TimeOnly(13, 0), critical);
        Assert.Contains(new TimeOnly(11, 0), critical);
        Assert.DoesNotContain(new TimeOnly(13, 30), critical);
        Assert.Equal(2, critical.Count);
    }

    [Fact]
    public void GetBreakOffsetBucket_classifies_early_ideal_late()
    {
        var start = new TimeOnly(8, 0);
        Assert.Equal(BreakSlotPlanner.BreakOffsetBucket.Early,
            BreakSlotPlanner.GetBreakOffsetBucket(start, new TimeOnly(11, 0)));
        Assert.Equal(BreakSlotPlanner.BreakOffsetBucket.Ideal,
            BreakSlotPlanner.GetBreakOffsetBucket(start, new TimeOnly(12, 0)));
        Assert.Equal(BreakSlotPlanner.BreakOffsetBucket.Late,
            BreakSlotPlanner.GetBreakOffsetBucket(start, new TimeOnly(13, 0)));
    }

    [Fact]
    public void IsExtremeCaseBreak_only_plus_3_and_plus_5()
    {
        var start = new TimeOnly(9, 0);
        Assert.True(BreakSlotPlanner.IsExtremeCaseBreak(start, start.AddHours(3)));
        Assert.True(BreakSlotPlanner.IsExtremeCaseBreak(start, start.AddHours(5)));
        Assert.False(BreakSlotPlanner.IsExtremeCaseBreak(start, start.AddHours(5.5)));
        Assert.False(BreakSlotPlanner.IsExtremeCaseBreak(start, start.AddHours(6)));
        Assert.False(BreakSlotPlanner.IsExtremeCaseBreak(start, start.AddHours(4)));
    }

    [Fact]
    public void NormalizeSlots_rejects_beyond_plus_5h()
    {
        var start = new TimeOnly(9, 0);
        var slots = BreakSlotPlanner.NormalizeSlots(start, true, ["16:00", "14:00", "13:00"]);
        Assert.Contains(new TimeOnly(14, 0), slots); // +5h
        Assert.DoesNotContain(new TimeOnly(16, 0), slots); // +7h interdit
    }

    [Fact]
    public void ClampMinPresence_zero_means_disabled()
    {
        Assert.Equal(0, BreakSlotPlanner.ClampMinPresence(0));
        Assert.Equal(0, BreakSlotPlanner.ClampMinPresence(-1));
        Assert.Equal(50, BreakSlotPlanner.ClampMinPresence(40));
        Assert.Equal(70, BreakSlotPlanner.ClampMinPresence(70));
        Assert.Equal(95, BreakSlotPlanner.ClampMinPresence(100));
    }
}
