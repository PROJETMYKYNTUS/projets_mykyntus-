using System.Text.Json;
using Planning.Domain.Entities;

namespace Planning.Infrastructure.Services;

/// <summary>
/// Break slots: 1h, idéal start+4h.
/// Non-critique : préféré +3h30…+4h30 ; +3h/+5h en secours.
/// Critique : vagues Opening 11h→midi→13h, autres souvent +5h, +4h dès que possible ;
/// plafond métier +5h (jamais +5h30/+6h/+7h).
/// </summary>
public static class BreakSlotPlanner
{
    public const int BreakDurationMinutes = 60;
    public const int MaxSlots = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Buckets d’équité / diversité des pauses (cellule critique).</summary>
    public enum BreakOffsetBucket
    {
        Early,  // +3h
        Ideal,  // +4h / +4h30 / bande préférée
        Late    // +5h (plafond métier)
    }

    /// <summary>8h/9h → tôt ; 10h/11h → tard (midi biologique).</summary>
    public static bool IsEarlyShift(TimeOnly shiftStart) => shiftStart.Hour < 10;

    /// <summary>Opening métier : shifts 8h et 9h.</summary>
    public static bool IsOpeningShift(TimeOnly shiftStart) =>
        shiftStart.Hour is 8 or 9;

    public static BreakOffsetBucket GetBreakOffsetBucket(TimeOnly shiftStart, TimeOnly breakStart)
    {
        var hours = (breakStart - shiftStart).TotalHours;
        if (hours < 3.5 - 1e-6) return BreakOffsetBucket.Early;
        if (hours <= 4.5 + 1e-6) return BreakOffsetBucket.Ideal;
        return BreakOffsetBucket.Late;
    }

    public static List<TimeOnly> NormalTier(TimeOnly shiftStart) =>
        new()
        {
            shiftStart.AddHours(4),
            shiftStart.AddHours(4.5)
        };

    public static List<TimeOnly> ExtendedTier(TimeOnly shiftStart) =>
        new() { shiftStart.AddHours(3.5) };

    /// <summary>
    /// Extrêmes : +3h/+5h (plafond métier +5h, pas de +5h30/+6h).
    /// Ordre tôt = late-first, tard = early-first.
    /// </summary>
    public static List<TimeOnly> ExtremeTier(
        TimeOnly shiftStart,
        bool isCriticalCell = false,
        bool isOpeningShift = false)
    {
        _ = isCriticalCell;
        _ = isOpeningShift;
        if (IsEarlyShift(shiftStart))
            return new List<TimeOnly> { shiftStart.AddHours(5), shiftStart.AddHours(3) };
        return new List<TimeOnly> { shiftStart.AddHours(3), shiftStart.AddHours(5) };
    }

    /// <summary>
    /// Fenêtre non extrême : +3h30 → +4h30 par pas de 15 min (sauts tolérés entre créneaux).
    /// </summary>
    public static List<TimeOnly> PreferredSlots(TimeOnly shiftStart)
    {
        var slots = new List<TimeOnly>();
        var start = shiftStart.AddHours(3.5);
        var end = shiftStart.AddHours(4.5);
        for (var t = start; t <= end; t = t.AddMinutes(15))
            slots.Add(t);
        return slots;
    }

    /// <summary>
    /// Ordre packing : idéaux +4/+4h30 puis reste préféré, puis extrêmes.
    /// </summary>
    public static List<TimeOnly> ProgressiveOpenOrder(
        TimeOnly shiftStart,
        bool isCriticalCell = false,
        bool isOpeningShift = false)
    {
        var order = new List<TimeOnly>
        {
            shiftStart.AddHours(4),
            shiftStart.AddHours(4.5),
        };
        foreach (var s in PreferredSlots(shiftStart))
        {
            if (!order.Contains(s))
                order.Add(s);
        }

        foreach (var s in ExtremeTier(shiftStart, isCriticalCell, isOpeningShift))
        {
            if (!order.Contains(s))
                order.Add(s);
        }

        return order;
    }

    public static List<List<TimeOnly>> BreakTiers(
        TimeOnly shiftStart,
        bool isCriticalCell = false,
        bool isOpeningShift = false) =>
        new()
        {
            PreferredSlots(shiftStart),
            ExtremeTier(shiftStart, isCriticalCell, isOpeningShift),
        };

    public static List<TimeOnly> BuildPreferredBreakSlots(TimeOnly shiftStart, bool isCriticalCell)
    {
        var progressive = ProgressiveOpenOrder(shiftStart, isCriticalCell);
        if (isCriticalCell)
            return progressive.Take(MaxSlots).ToList();

        return progressive.Take(2).ToList();
    }

    public static List<TimeOnly> BuildBreakAssignmentOrder(SubServiceShiftConfig config) =>
        ProgressiveOpenOrder(config.StartTime, config.IsCriticalCell);

    public static List<List<TimeOnly>> BuildBreakTiers(SubServiceShiftConfig config) =>
        BreakTiers(config.StartTime, config.IsCriticalCell);

    public static bool IsExtremeBreak(
        TimeOnly shiftStart,
        TimeOnly breakStart,
        bool isCriticalCell = true,
        bool isOpeningShift = true) =>
        ExtremeTier(shiftStart, isCriticalCell, isOpeningShift).Contains(breakStart);

    public static bool IsExtendedBreak(TimeOnly shiftStart, TimeOnly breakStart) =>
        ExtendedTier(shiftStart).Contains(breakStart);

    public static bool IsNonNormalBreak(TimeOnly shiftStart, TimeOnly breakStart) =>
        IsExtremeBreak(shiftStart, breakStart) || IsExtendedBreak(shiftStart, breakStart);

    /// <summary>Cas extrême métier : pause à +3h ou +5h du start (plafond +5h).</summary>
    public static bool IsExtremeCaseBreak(TimeOnly shiftStart, TimeOnly breakStart)
    {
        var hours = (breakStart - shiftStart).TotalHours;
        return Math.Abs(hours - 3.0) < 0.001
            || Math.Abs(hours - 5.0) < 0.001;
    }

    /// <summary>Retire +3h / +5h des candidats (cas particuliers).</summary>
    public static List<TimeOnly> WithoutExtremeCaseBreaks(
        TimeOnly shiftStart, IEnumerable<TimeOnly> candidates)
    {
        return candidates
            .Where(s => !IsExtremeCaseBreak(shiftStart, s))
            .ToList();
    }

    public static TimeOnly WindowStart(TimeOnly shiftStart, bool isCriticalCell) =>
        isCriticalCell ? shiftStart.AddHours(3) : shiftStart.AddHours(4);

    /// <summary>
    /// Fin de fenêtre : non-critique +4h30 ; critique +5h max (jamais +5h30/+6h/+7h).
    /// </summary>
    public static TimeOnly WindowEnd(
        TimeOnly shiftStart,
        bool isCriticalCell,
        bool isOpeningShift = false)
    {
        _ = isOpeningShift;
        if (!isCriticalCell) return shiftStart.AddHours(4.5);
        return shiftStart.AddHours(5);
    }

    /// <summary>
    /// Fenêtre autorisée dense (15 min) de WindowStart…WindowEnd.
    /// </summary>
    public static List<TimeOnly> DenseWindowSlots(
        TimeOnly shiftStart,
        bool isCriticalCell,
        int stepMinutes = 15,
        bool isOpeningShift = false)
    {
        var step = stepMinutes > 0 ? stepMinutes : 15;
        var start = WindowStart(shiftStart, isCriticalCell);
        var end = WindowEnd(shiftStart, isCriticalCell, isOpeningShift);
        var slots = new List<TimeOnly>();
        for (var t = start; t <= end; t = t.AddMinutes(step))
            slots.Add(t);
        return slots;
    }

    /// <summary>
    /// Candidats packing : idéaux → préférés → reste fenêtre dense (non-extrême) → extrêmes (+3h/+5h).
    /// Si <paramref name="excludeExtremes"/> : jamais +3h/+5h.
    /// </summary>
    public static List<TimeOnly> PackingCandidates(
        TimeOnly shiftStart, bool isCriticalCell, bool excludeExtremes = false)
    {
        var order = new List<TimeOnly>();
        void Add(TimeOnly s)
        {
            if (!order.Contains(s))
                order.Add(s);
        }

        Add(shiftStart.AddHours(4));
        Add(shiftStart.AddHours(4.5));
        foreach (var s in PreferredSlots(shiftStart))
            Add(s);

        var extremes = ExtremeTier(shiftStart, isCriticalCell);
        var extremeSet = extremes.ToHashSet();
        foreach (var s in DenseWindowSlots(shiftStart, isCriticalCell))
        {
            if (!extremeSet.Contains(s))
                Add(s);
        }

        if (!excludeExtremes)
        {
            foreach (var s in extremes)
                Add(s);
        }

        return order;
    }

    /// <summary>
    /// Vague Opening critique — phase 11h : uniquement le début de fenêtre (+3h / +3h30).
    /// </summary>
    public static List<TimeOnly> PackingCandidatesOpeningExclusive(TimeOnly shiftStart, bool isCriticalCell)
    {
        if (!isCriticalCell)
            return PackingCandidates(shiftStart, false);
        var order = new List<TimeOnly>();
        void Add(TimeOnly s)
        {
            if (!order.Contains(s)) order.Add(s);
        }
        Add(shiftStart.AddHours(3));      // 11:00
        Add(shiftStart.AddHours(3.5));    // 11:30
        return order;
    }

    /// <summary>
    /// Vague Opening critique — suite : midi → 13h (+5h max, grille 30 min).
    /// </summary>
    public static List<TimeOnly> PackingCandidatesEarlyWave(TimeOnly shiftStart, bool isCriticalCell)
    {
        var order = new List<TimeOnly>();
        void Add(TimeOnly s)
        {
            if (!order.Contains(s))
                order.Add(s);
        }

        if (!isCriticalCell)
            return PackingCandidates(shiftStart, isCriticalCell: false);

        // 11h d'abord (si appelé hors phase exclusive)
        Add(shiftStart.AddHours(3));
        Add(shiftStart.AddHours(3.5));
        Add(shiftStart.AddHours(4));
        Add(shiftStart.AddHours(4.5));
        Add(shiftStart.AddHours(5));

        foreach (var s in DenseWindowSlots(shiftStart, isCriticalCell: true, stepMinutes: 30, isOpeningShift: true))
            Add(s);

        return order;
    }

    /// <summary>
    /// Autres shifts critique : +5h puis +4h, grille 30 min, plafond +5h.
    /// </summary>
    public static List<TimeOnly> PackingCandidatesSpread(TimeOnly shiftStart, bool isCriticalCell)
    {
        var order = new List<TimeOnly>();
        void Add(TimeOnly s)
        {
            if (!order.Contains(s))
                order.Add(s);
        }

        if (!isCriticalCell)
            return PackingCandidates(shiftStart, isCriticalCell: false);

        var opening = IsOpeningShift(shiftStart);

        Add(shiftStart.AddHours(5));
        Add(shiftStart.AddHours(4));
        Add(shiftStart.AddHours(4.5));
        Add(shiftStart.AddHours(3));
        Add(shiftStart.AddHours(3.5));

        foreach (var s in DenseWindowSlots(shiftStart, isCriticalCell: true, stepMinutes: 30, isOpeningShift: opening))
            Add(s);

        return order;
    }

    public static List<TimeOnly> AllowedStarts(
        TimeOnly shiftStart,
        bool isCriticalCell,
        bool isOpeningShift = false)
    {
        var start = WindowStart(shiftStart, isCriticalCell);
        var end = WindowEnd(shiftStart, isCriticalCell, isOpeningShift);
        var slots = new List<TimeOnly>();
        var current = start;
        while (current <= end)
        {
            slots.Add(current);
            current = current.AddMinutes(30);
        }

        return slots;
    }

    public static List<TimeOnly> NormalizeSlots(
        TimeOnly shiftStart,
        bool isCriticalCell,
        IEnumerable<string>? rawSlots)
    {
        var allowed = AllowedStarts(shiftStart, isCriticalCell).ToHashSet();
        var parsed = new List<TimeOnly>();

        if (rawSlots != null)
        {
            foreach (var raw in rawSlots)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                if (!TimeOnly.TryParse(raw.Trim(), out var t)) continue;
                if (!allowed.Contains(t)) continue;
                if (!parsed.Contains(t))
                    parsed.Add(t);
                if (parsed.Count >= MaxSlots) break;
            }
        }

        if (parsed.Count == 0)
            return OrderByIdeal(BuildPreferredBreakSlots(shiftStart, isCriticalCell), shiftStart, isCriticalCell);

        return OrderByIdeal(parsed, shiftStart, isCriticalCell);
    }

    public static List<TimeOnly> ResolveBreakSlots(SubServiceShiftConfig config)
    {
        var fromJson = DeserializeSlots(config.BreakSlotsJson);
        if (fromJson.Count > 0)
        {
            return NormalizeSlots(config.StartTime, config.IsCriticalCell,
                fromJson.Select(t => t.ToString("HH:mm")));
        }

        var allowed = AllowedStarts(config.StartTime, config.IsCriticalCell).ToHashSet();
        var fromRange = new List<TimeOnly>();
        var current = config.BreakRangeStart;
        var rangeEnd = config.BreakRangeEnd;
        if (rangeEnd <= current)
            rangeEnd = current.AddMinutes(BreakDurationMinutes);

        while (current < rangeEnd)
        {
            if (allowed.Contains(current) && !fromRange.Contains(current))
                fromRange.Add(current);
            current = current.AddMinutes(30);
        }

        if (fromRange.Count == 0)
            return OrderByIdeal(
                BuildPreferredBreakSlots(config.StartTime, config.IsCriticalCell),
                config.StartTime,
                config.IsCriticalCell);

        return OrderByIdeal(fromRange.Take(MaxSlots).ToList(), config.StartTime, config.IsCriticalCell);
    }

    public static string SerializeSlots(IReadOnlyList<TimeOnly> slots) =>
        JsonSerializer.Serialize(slots.Select(s => s.ToString("HH:mm")).ToList(), JsonOptions);

    public static List<TimeOnly> DeserializeSlots(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<TimeOnly>();

        try
        {
            var raw = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
            if (raw == null || raw.Count == 0)
                return new List<TimeOnly>();

            var result = new List<TimeOnly>();
            foreach (var s in raw)
            {
                if (TimeOnly.TryParse(s, out var t) && !result.Contains(t))
                    result.Add(t);
            }

            return result;
        }
        catch
        {
            return new List<TimeOnly>();
        }
    }

    public static (TimeOnly RangeStart, TimeOnly RangeEnd) SyncRange(
        IReadOnlyList<TimeOnly> slots,
        int durationMinutes = BreakDurationMinutes)
    {
        if (slots.Count == 0)
            throw new ArgumentException("At least one break slot is required.", nameof(slots));

        var duration = durationMinutes > 0 ? durationMinutes : BreakDurationMinutes;
        var min = slots.Min();
        var max = slots.Max();
        return (min, max.AddMinutes(duration));
    }

    public static List<TimeOnly> OrderByIdeal(
        IEnumerable<TimeOnly> slots,
        TimeOnly shiftStart,
        bool isCriticalCell = false)
    {
        var priority = ProgressiveOpenOrder(shiftStart, isCriticalCell);
        return slots
            .Distinct()
            .OrderBy(s =>
            {
                var idx = priority.IndexOf(s);
                return idx >= 0 ? idx : 1000 + DistanceMinutes(s, shiftStart.AddHours(4));
            })
            .ThenBy(s => s)
            .Take(MaxSlots)
            .ToList();
    }

    public static int DistanceMinutes(TimeOnly a, TimeOnly b) =>
        Math.Abs((int)(a.ToTimeSpan() - b.ToTimeSpan()).TotalMinutes);

    public static int ClampMinPresence(int minPresencePercent)
    {
        // 0 = présence min désactivée pour la cellule (aucune exigence plateau).
        if (minPresencePercent <= 0) return 0;
        return Math.Clamp(minPresencePercent, 50, 95);
    }
}
