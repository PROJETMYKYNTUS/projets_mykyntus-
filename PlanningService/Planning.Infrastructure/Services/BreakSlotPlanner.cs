using System.Text.Json;
using Planning.Domain.Entities;

namespace Planning.Infrastructure.Services;

/// <summary>
/// Break slots: 1h, idéal start+4h. Packing anti-extrême pour TOUS les shifts.
/// Fenêtre préférée +3h30…+4h30 (15 min) ; +3h/+5h en dernier recours.
/// </summary>
public static class BreakSlotPlanner
{
    public const int BreakDurationMinutes = 60;
    public const int MaxSlots = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>8h/9h → tôt ; 10h/11h → tard (midi biologique).</summary>
    public static bool IsEarlyShift(TimeOnly shiftStart) => shiftStart.Hour < 10;

    public static List<TimeOnly> NormalTier(TimeOnly shiftStart) =>
        new()
        {
            shiftStart.AddHours(4),
            shiftStart.AddHours(4.5)
        };

    public static List<TimeOnly> ExtendedTier(TimeOnly shiftStart) =>
        new() { shiftStart.AddHours(3.5) };

    public static List<TimeOnly> ExtremeTier(TimeOnly shiftStart) =>
        IsEarlyShift(shiftStart)
            ? new List<TimeOnly> { shiftStart.AddHours(5), shiftStart.AddHours(3) }
            : new List<TimeOnly> { shiftStart.AddHours(3), shiftStart.AddHours(5) };

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
    /// Sauts autorisés — objectif = minimiser +3h/+5h.
    /// </summary>
    public static List<TimeOnly> ProgressiveOpenOrder(TimeOnly shiftStart)
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

        foreach (var s in ExtremeTier(shiftStart))
        {
            if (!order.Contains(s))
                order.Add(s);
        }

        return order;
    }

    public static List<List<TimeOnly>> BreakTiers(TimeOnly shiftStart) =>
        new()
        {
            PreferredSlots(shiftStart),
            ExtremeTier(shiftStart),
        };

    public static List<TimeOnly> BuildPreferredBreakSlots(TimeOnly shiftStart, bool isCriticalCell)
    {
        var progressive = ProgressiveOpenOrder(shiftStart);
        if (isCriticalCell)
            return progressive.Take(MaxSlots).ToList();

        return progressive.Take(2).ToList();
    }

    public static List<TimeOnly> BuildBreakAssignmentOrder(SubServiceShiftConfig config) =>
        ProgressiveOpenOrder(config.StartTime);

    public static List<List<TimeOnly>> BuildBreakTiers(SubServiceShiftConfig config) =>
        BreakTiers(config.StartTime);

    public static bool IsExtremeBreak(TimeOnly shiftStart, TimeOnly breakStart) =>
        ExtremeTier(shiftStart).Contains(breakStart);

    public static bool IsExtendedBreak(TimeOnly shiftStart, TimeOnly breakStart) =>
        ExtendedTier(shiftStart).Contains(breakStart);

    public static bool IsNonNormalBreak(TimeOnly shiftStart, TimeOnly breakStart) =>
        IsExtremeBreak(shiftStart, breakStart) || IsExtendedBreak(shiftStart, breakStart);

    /// <summary>Cas extrême métier : pause à +3h ou +5h du start (tous shifts).</summary>
    public static bool IsExtremeCaseBreak(TimeOnly shiftStart, TimeOnly breakStart)
    {
        var hours = (breakStart - shiftStart).TotalHours;
        return Math.Abs(hours - 3.0) < 0.001 || Math.Abs(hours - 5.0) < 0.001;
    }

    public static TimeOnly WindowStart(TimeOnly shiftStart, bool isCriticalCell) =>
        isCriticalCell ? shiftStart.AddHours(3) : shiftStart.AddHours(4);

    public static TimeOnly WindowEnd(TimeOnly shiftStart, bool isCriticalCell) =>
        isCriticalCell ? shiftStart.AddHours(5) : shiftStart.AddHours(4.5);

    public static List<TimeOnly> AllowedStarts(TimeOnly shiftStart, bool isCriticalCell)
    {
        var start = WindowStart(shiftStart, isCriticalCell);
        var end = WindowEnd(shiftStart, isCriticalCell);
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
            return OrderByIdeal(BuildPreferredBreakSlots(shiftStart, isCriticalCell), shiftStart);

        return OrderByIdeal(parsed, shiftStart);
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
            return OrderByIdeal(BuildPreferredBreakSlots(config.StartTime, config.IsCriticalCell), config.StartTime);

        return OrderByIdeal(fromRange.Take(MaxSlots).ToList(), config.StartTime);
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

    public static List<TimeOnly> OrderByIdeal(IEnumerable<TimeOnly> slots, TimeOnly shiftStart)
    {
        var priority = ProgressiveOpenOrder(shiftStart);
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
        if (minPresencePercent <= 0) return 70;
        return Math.Clamp(minPresencePercent, 50, 95);
    }
}
