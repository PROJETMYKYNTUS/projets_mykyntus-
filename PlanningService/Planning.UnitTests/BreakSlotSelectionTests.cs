using Planning.Domain.Entities;
using Planning.Domain.Enums;
using Planning.Infrastructure.Services;

namespace Planning.UnitTests;

public class BreakSlotSelectionTests
{
    private static SubServiceShiftConfig MakeConfig(
        int id, TimeOnly start, bool critical, IReadOnlyList<TimeOnly> slots) => new()
    {
        Id = id,
        SubServiceId = 1,
        Label = $"Shift {id}",
        StartTime = start,
        WorkHours = 8,
        BreakDurationMinutes = 60,
        IsCriticalCell = critical,
        MinPresencePercent = 70,
        DisplayOrder = id,
        BreakSlotsJson = BreakSlotPlanner.SerializeSlots(slots),
        CreatedAt = DateTime.UtcNow
    };

    private static List<ShiftAssignment> Agents(int count, int shiftId, DateOnly date)
    {
        var list = new List<ShiftAssignment>();
        for (var i = 1; i <= count; i++)
        {
            list.Add(new ShiftAssignment
            {
                UserId = i,
                SubServiceShiftConfigId = shiftId,
                AssignedDate = date,
                DayOfWeek = DayOfWeekEnum.Monday
            });
        }
        return list;
    }

    [Fact]
    public void AssignDayBreaks_uses_only_checked_band_non_critical()
    {
        var start = new TimeOnly(8, 0);
        var noon = new TimeOnly(12, 0);
        var config = MakeConfig(1, start, critical: false, [noon]);
        var configs = new Dictionary<int, SubServiceShiftConfig> { [1] = config };
        var assignments = Agents(4, 1, new DateOnly(2026, 8, 3));

        PlateauBreakPacker.AssignDayBreaks(assignments, configs, 70);

        Assert.All(assignments, a => Assert.Equal(noon, a.BreakTime));
        Assert.DoesNotContain(assignments, a => a.BreakTime == new TimeOnly(12, 30));
    }

    [Fact]
    public void AssignDayBreaks_critical_never_uses_unchecked_noon()
    {
        var start = new TimeOnly(8, 0);
        var eleven = new TimeOnly(11, 0);
        var thirteen = new TimeOnly(13, 0);
        var noon = new TimeOnly(12, 0);
        var config = MakeConfig(1, start, critical: true, [eleven, thirteen]);
        var configs = new Dictionary<int, SubServiceShiftConfig> { [1] = config };
        var assignments = Agents(8, 1, new DateOnly(2026, 8, 3));

        PlateauBreakPacker.AssignDayBreaks(assignments, configs, 70);

        Assert.All(assignments, a =>
        {
            Assert.True(a.BreakTime == eleven || a.BreakTime == thirteen, $"got {a.BreakTime}");
        });
        Assert.DoesNotContain(assignments, a => a.BreakTime == noon);
    }

    [Fact]
    public void AssignDayBreaks_single_band_high_headcount_stays_strict_and_warns()
    {
        var start = new TimeOnly(8, 0);
        var noon = new TimeOnly(12, 0);
        var config = MakeConfig(1, start, critical: false, [noon]);
        var configs = new Dictionary<int, SubServiceShiftConfig> { [1] = config };
        var date = new DateOnly(2026, 8, 4);
        var assignments = Agents(12, 1, date);

        PlateauBreakPacker.AssignDayBreaks(assignments, configs, 80);

        Assert.All(assignments, a => Assert.Equal(noon, a.BreakTime));
        var warnings = PlateauBreakPacker.BuildRestrictedBandWarnings(
            assignments, configs, 80, "Tuesday", date);
        Assert.NotEmpty(warnings);
        Assert.Contains(warnings, w => w.Contains("bandes de pause trop restreintes"));
    }

    [Fact]
    public void ResolveBreakSlots_empty_json_keeps_default_window()
    {
        var config = new SubServiceShiftConfig
        {
            Id = 1,
            SubServiceId = 1,
            Label = "8h",
            StartTime = new TimeOnly(8, 0),
            WorkHours = 8,
            IsCriticalCell = false,
            BreakSlotsJson = null,
            CreatedAt = DateTime.UtcNow
        };

        var slots = BreakSlotPlanner.ResolveBreakSlots(config);
        Assert.Contains(new TimeOnly(12, 0), slots);
        Assert.Contains(new TimeOnly(12, 30), slots);
    }

    [Fact]
    public void NormalizeSlots_keeps_more_than_three_checked_bands()
    {
        var start = new TimeOnly(8, 0);
        var slots = BreakSlotPlanner.NormalizeSlots(start, true, ["11:00", "11:30", "12:00", "12:30", "13:00"]);
        Assert.Equal(5, slots.Count);
    }

    [Fact]
    public void KeepAllowed_drops_unchecked_candidates()
    {
        var config = MakeConfig(1, new TimeOnly(8, 0), false, [new TimeOnly(12, 0)]);
        var kept = BreakSlotPlanner.KeepAllowed(
            config,
            new[] { new TimeOnly(12, 0), new TimeOnly(12, 30), new TimeOnly(13, 0) });
        Assert.Equal(new TimeOnly(12, 0), Assert.Single(kept));
    }
}
