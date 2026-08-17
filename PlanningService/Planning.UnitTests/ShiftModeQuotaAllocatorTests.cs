using Planning.Infrastructure.Services;

namespace Planning.UnitTests;

public class ShiftModeQuotaAllocatorTests
{
    [Fact]
    public void AllocateCounts_SumsToHeadcount()
    {
        var counts = ShiftModeQuotaAllocator.AllocateCounts([40m, 35m, 25m], 10);
        Assert.Equal(3, counts.Length);
        Assert.Equal(10, counts.Sum());
        Assert.Equal(4, counts[0]);
        Assert.Equal(4, counts[1]); // 3.5 → floor 3 + remainder
        Assert.Equal(2, counts[2]);
    }

    [Fact]
    public void AllocateCounts_EmptyOrZero_ReturnsZeros()
    {
        Assert.Equal(new[] { 0, 0 }, ShiftModeQuotaAllocator.AllocateCounts([50m, 50m], 0));
        Assert.Empty(ShiftModeQuotaAllocator.AllocateCounts([], 5));
    }

    [Fact]
    public void AllocateCounts_DeterministicLargestRemainder()
    {
        var a = ShiftModeQuotaAllocator.AllocateCounts([33.3m, 33.3m, 33.4m], 7);
        var b = ShiftModeQuotaAllocator.AllocateCounts([33.3m, 33.3m, 33.4m], 7);
        Assert.Equal(a, b);
        Assert.Equal(7, a.Sum());
    }
}
