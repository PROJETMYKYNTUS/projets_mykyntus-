using Planning.Domain.Entities;
using Planning.Infrastructure.Services;

namespace Planning.UnitTests;

public class SaturdayQuotaTests
{
    [Theory]
    [InlineData(20, 10)]
    [InlineData(19, 9)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    [InlineData(2, 1)]
    public void SaturdayRequiredCount_is_half_of_week_quota(int week, int expected)
        => Assert.Equal(expected, ShiftDispersionSelector.SaturdayRequiredCount(week));

    [Fact]
    public void SelectSaturday_fills_opening_quota_before_overflowing_next_shift()
    {
        var opening = new SubServiceShiftConfig
        {
            Id = 1, Label = "8h", StartTime = new TimeOnly(8, 0),
            RequiredCount = 20, DisplayOrder = 1
        };
        var mid = new SubServiceShiftConfig
        {
            Id = 2, Label = "9h", StartTime = new TimeOnly(9, 0),
            RequiredCount = 10, DisplayOrder = 2
        };
        var ordered = new List<SubServiceShiftConfig> { opening, mid };
        var counts = new Dictionary<int, int> { [1] = 0, [2] = 0 };
        var history = new Dictionary<int, List<int>>();

        // Remplir le quota samedi opening (10) avant d'envoyer sur le 2e
        for (var i = 0; i < 10; i++)
        {
            history[i] = new List<int>();
            var chosen = ShiftDispersionSelector.SelectSaturday(
                ordered, preferredIndex: 0, fridayShiftId: null,
                history, userId: i, shiftCountToday: counts);
            Assert.Equal(1, chosen.Id);
            counts[1]++;
        }

        history[100] = new List<int>();
        var next = ShiftDispersionSelector.SelectSaturday(
            ordered, preferredIndex: 0, fridayShiftId: null,
            history, userId: 100, shiftCountToday: counts);
        // Quota opening samedi atteint (10) → peut aller sur mid
        Assert.Equal(2, next.Id);
    }
}
