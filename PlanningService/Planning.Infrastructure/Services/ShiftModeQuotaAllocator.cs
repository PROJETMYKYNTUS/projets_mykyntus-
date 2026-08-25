namespace Planning.Infrastructure.Services;

/// <summary>Répartition déterministe des effectifs depuis des pourcentages (plus grands restes).</summary>
public static class ShiftModeQuotaAllocator
{
    /// <summary>
    /// Quotas journaliers d'un mode multi-shift.
    /// Solo (1 personne, &gt;1 shift) : tous à 0 — l'alternance semaine remplace les %.
    /// </summary>
    public static int[] AllocateModeRequiredCounts(IReadOnlyList<decimal> percentages, int headcount)
    {
        if (percentages.Count == 0)
            return [];
        if (headcount == 1 && percentages.Count > 1)
            return percentages.Select(_ => 0).ToArray();
        return AllocateCounts(percentages, headcount);
    }

    public static int[] AllocateCounts(IReadOnlyList<decimal> percentages, int totalHeadcount)
    {
        if (totalHeadcount <= 0 || percentages.Count == 0)
            return percentages.Select(_ => 0).ToArray();

        var raw = percentages.Select(p => Math.Max(0m, p) / 100m * totalHeadcount).ToArray();
        var floors = raw.Select(v => (int)Math.Floor(v)).ToArray();
        var assigned = floors.Sum();
        var remaining = totalHeadcount - assigned;

        var order = raw
            .Select((v, i) => (Frac: v - floors[i], Index: i))
            .OrderByDescending(x => x.Frac)
            .ThenBy(x => x.Index)
            .Select(x => x.Index)
            .ToList();

        for (var i = 0; i < remaining; i++)
            floors[order[i % order.Count]]++;

        return floors;
    }
}
