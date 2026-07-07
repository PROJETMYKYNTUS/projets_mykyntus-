namespace Planning.Infrastructure.Services;

/// <summary>Compte les jours d'absence lun–sam (dimanche exclu) sur une période mensuelle.</summary>
public static class PlanningAbsenceDayCounter
{
    public static bool TryParsePrimePeriod(string period, out DateOnly monthStart, out DateOnly monthEnd)
    {
        monthStart = default;
        monthEnd = default;
        var m = System.Text.RegularExpressions.Regex.Match(period.Trim(), @"^(\d{4})-(\d{2})$");
        if (!m.Success) return false;
        var year = int.Parse(m.Groups[1].Value);
        var month = int.Parse(m.Groups[2].Value);
        if (month is < 1 or > 12) return false;
        monthStart = new DateOnly(year, month, 1);
        monthEnd = monthStart.AddMonths(1).AddDays(-1);
        return true;
    }

    /// <summary>Union des jours lun–sam pour plusieurs plages, intersectées avec [monthStart, monthEnd].</summary>
    public static int CountUnionMonToSatDays(
        IEnumerable<(DateOnly Start, DateOnly End)> ranges,
        DateOnly monthStart,
        DateOnly monthEnd)
    {
        var union = new HashSet<DateOnly>();
        foreach (var (rangeStart, rangeEnd) in ranges)
        {
            var start = rangeStart > monthStart ? rangeStart : monthStart;
            var end = rangeEnd < monthEnd ? rangeEnd : monthEnd;
            if (start > end) continue;

            for (var d = start; d <= end; d = d.AddDays(1))
            {
                if (d.DayOfWeek != DayOfWeek.Sunday)
                    union.Add(d);
            }
        }

        return union.Count;
    }
}
