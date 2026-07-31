using System.Text.Json;
using Planning.Domain.Entities;

namespace Planning.Infrastructure.Services;

/// <summary>
/// Break slots: 1h duration, ideal around start+4h.
/// Normal: only +4h / +4h30. Extremes [+3h,+5h] only if IsCriticalCell.
/// </summary>
public static class BreakSlotPlanner
{
    public const int BreakDurationMinutes = 60;
    public const int MaxSlots = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static List<TimeOnly> BuildPreferredBreakSlots(TimeOnly shiftStart, bool isCriticalCell)
    {
        if (isCriticalCell)
        {
            return new List<TimeOnly>
            {
                shiftStart.AddHours(3.5),
                shiftStart.AddHours(4),
                shiftStart.AddHours(4.5)
            };
        }

        return new List<TimeOnly>
        {
            shiftStart.AddHours(4),
            shiftStart.AddHours(4.5)
        };
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

        // Legacy: derive up to 3 slots from the saved range, within the allowed window
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
        var ideal = shiftStart.AddHours(4);
        return slots
            .Distinct()
            .OrderBy(s => DistanceMinutes(s, ideal))
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
