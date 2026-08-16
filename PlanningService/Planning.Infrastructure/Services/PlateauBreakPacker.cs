using Planning.Domain.Entities;

namespace Planning.Infrastructure.Services;

/// <summary>
/// Packing des pauses aligné sur le KPI plateau P :
/// à chaque instant, (présent − en pause) / présent ≥ MinPresencePercent.
/// </summary>
public static class PlateauBreakPacker
{
    public const int SampleStepMinutes = 5;

    /// <summary>Compteurs semaine pour diversifier Early (+3h) / Late (+5h) entre pilotes.</summary>
    public sealed class BreakFairnessCounters
    {
        public Dictionary<int, int> EarlyCounts { get; } = new();
        public Dictionary<int, int> LateCounts { get; } = new();

        public void Record(int userId, TimeOnly shiftStart, TimeOnly breakStart)
        {
            var bucket = BreakSlotPlanner.GetBreakOffsetBucket(shiftStart, breakStart);
            if (bucket == BreakSlotPlanner.BreakOffsetBucket.Early)
                EarlyCounts[userId] = EarlyCounts.GetValueOrDefault(userId) + 1;
            else if (bucket == BreakSlotPlanner.BreakOffsetBucket.Late)
                LateCounts[userId] = LateCounts.GetValueOrDefault(userId) + 1;
        }
    }

    public readonly record struct ShiftRange(TimeOnly Start, TimeOnly End);

    public readonly record struct BreakPlacement(TimeOnly Start, int DurationMinutes);

    public static int MaxOnBreakAt(int presentAt, int minPresencePercent)
    {
        if (presentAt <= 0) return 0;
        var minPresence = BreakSlotPlanner.ClampMinPresence(minPresencePercent);
        // Présence min désactivée : pas de plafond pause.
        if (minPresence <= 0) return presentAt;
        var max = (int)Math.Floor(presentAt * (100 - minPresence) / 100.0);
        if (max == 0 && presentAt > 1)
        {
            var pctIfOne = (presentAt - 1) * 100.0 / presentAt;
            if (pctIfOne + 1e-9 >= minPresence)
                max = 1;
        }

        // Slack critique : +1 siège seulement s'il conserve un plancher ~83 %
        // (évite 4/20 = 80 % après départ Opening).
        if (minPresence >= 80 && max >= 1 && presentAt >= 20)
        {
            var withSlack = max + 1;
            var pct = (presentAt - withSlack) * 100.0 / presentAt;
            if (pct + 1e-9 >= 83.0)
                max = withSlack;
        }

        return max;
    }

    public static int PresentAt(IReadOnlyList<ShiftRange> ranges, TimeOnly instant)
    {
        var n = 0;
        foreach (var r in ranges)
        {
            if (r.Start <= instant && r.End > instant)
                n++;
        }
        return n;
    }

    public static int CountOnBreakAt(
        IReadOnlyList<BreakPlacement> breaks,
        TimeOnly instant)
    {
        var n = 0;
        foreach (var b in breaks)
        {
            var dur = b.DurationMinutes > 0
                ? b.DurationMinutes
                : BreakSlotPlanner.BreakDurationMinutes;
            if (b.Start <= instant && b.Start.AddMinutes(dur) > instant)
                n++;
        }
        return n;
    }

    /// <summary>
    /// True si ajouter la pause respecte le seuil sur toute sa durée (métrique P).
    /// </summary>
    public static bool FitsPresence(
        IReadOnlyList<ShiftRange> ranges,
        IReadOnlyList<BreakPlacement> already,
        TimeOnly candidate,
        int durationMinutes,
        int minPresencePercent)
    {
        var dur = durationMinutes > 0 ? durationMinutes : BreakSlotPlanner.BreakDurationMinutes;
        var end = candidate.AddMinutes(dur);
        for (var t = candidate; t < end; t = t.AddMinutes(SampleStepMinutes))
        {
            var present = PresentAt(ranges, t);
            if (present <= 0) continue;
            var maxBreak = MaxOnBreakAt(present, minPresencePercent);
            var onBreak = CountOnBreakAt(already, t) + 1;
            if (onBreak > maxBreak)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Min disponibilité % sur la fenêtre de la pause si on l'assigne (métrique P locale).
    /// </summary>
    public static decimal MinAvailabilityIfAssigned(
        IReadOnlyList<ShiftRange> ranges,
        IReadOnlyList<BreakPlacement> already,
        TimeOnly candidate,
        int durationMinutes)
    {
        var dur = durationMinutes > 0 ? durationMinutes : BreakSlotPlanner.BreakDurationMinutes;
        var end = candidate.AddMinutes(dur);
        decimal minPct = 100m;
        var any = false;
        for (var t = candidate; t < end; t = t.AddMinutes(SampleStepMinutes))
        {
            var present = PresentAt(ranges, t);
            if (present <= 0) continue;
            any = true;
            var onBreak = CountOnBreakAt(already, t) + 1;
            var available = Math.Max(0, present - onBreak);
            var pct = Math.Round((decimal)available / present * 100, 1);
            if (pct < minPct) minPct = pct;
        }
        return any ? minPct : 100m;
    }

    /// <summary>Effectif plateau min sur la durée de la pause candidate.</summary>
    public static int MinPresentDuring(
        IReadOnlyList<ShiftRange> ranges,
        TimeOnly candidate,
        int durationMinutes)
    {
        var dur = durationMinutes > 0 ? durationMinutes : BreakSlotPlanner.BreakDurationMinutes;
        var end = candidate.AddMinutes(dur);
        var minPresent = int.MaxValue;
        var any = false;
        for (var t = candidate; t < end; t = t.AddMinutes(SampleStepMinutes))
        {
            var present = PresentAt(ranges, t);
            if (present <= 0) continue;
            any = true;
            if (present < minPresent) minPresent = present;
        }
        return any ? minPresent : 0;
    }

    public static int PeakOnBreakIfAssigned(
        IReadOnlyList<BreakPlacement> already,
        TimeOnly candidate,
        int durationMinutes)
    {
        var dur = durationMinutes > 0 ? durationMinutes : BreakSlotPlanner.BreakDurationMinutes;
        var end = candidate.AddMinutes(dur);
        var peak = 0;
        for (var t = candidate; t < end; t = t.AddMinutes(SampleStepMinutes))
        {
            var onBreak = CountOnBreakAt(already, t) + 1;
            if (onBreak > peak) peak = onBreak;
        }
        return peak;
    }

    /// <summary>
    /// Choisit le meilleur créneau : d'abord ceux qui respectent le seuil,
    /// sinon le moindre mal (min pic).
    /// <paramref name="preferEarliest"/> : vague Opening critique (11h → midi → 13h).
    /// Sinon : min pic, puis rapprochement du +4h idéal si fourni, puis max min-P.
    /// </summary>
    public static TimeOnly? PickBreakSlot(
        IReadOnlyList<TimeOnly> candidates,
        IReadOnlyList<ShiftRange> ranges,
        IReadOnlyList<BreakPlacement> already,
        int durationMinutes,
        int minPresencePercent,
        out bool respectedThreshold,
        bool preferEarliest = false,
        TimeOnly? idealBreakStart = null)
    {
        respectedThreshold = false;
        if (candidates.Count == 0) return null;

        TimeOnly? bestFit = null;
        var bestFitPeak = int.MaxValue;
        var bestFitMinP = -1m;
        var bestFitPresent = -1;
        var bestFitIndex = int.MaxValue;
        var bestFitIdealDist = int.MaxValue;

        TimeOnly? bestFallback = null;
        var bestFallbackMinP = -1m;
        var bestFallbackPeak = int.MaxValue;
        var bestFallbackPresent = -1;
        var bestFallbackIndex = int.MaxValue;
        var bestFallbackIdealDist = int.MaxValue;

        for (var i = 0; i < candidates.Count; i++)
        {
            var slot = candidates[i];
            var minP = MinAvailabilityIfAssigned(ranges, already, slot, durationMinutes);
            var peak = PeakOnBreakIfAssigned(already, slot, durationMinutes);
            var minPresent = MinPresentDuring(ranges, slot, durationMinutes);
            var idealDist = idealBreakStart.HasValue
                ? Math.Abs((int)(slot.ToTimeSpan() - idealBreakStart.Value.ToTimeSpan()).TotalMinutes)
                : i;
            var fits = FitsPresence(ranges, already, slot, durationMinutes, minPresencePercent);

            if (fits)
            {
                bool better;
                if (preferEarliest)
                {
                    // Opening critique : 11h → midi → 13h (ordre candidats)
                    better = i < bestFitIndex;
                }
                else
                {
                    // Plateau d'abord (pic), puis +4h si possible, puis min-P
                    better = peak < bestFitPeak
                        || (peak == bestFitPeak && idealDist < bestFitIdealDist)
                        || (peak == bestFitPeak && idealDist == bestFitIdealDist && minP > bestFitMinP)
                        || (peak == bestFitPeak && idealDist == bestFitIdealDist && minP == bestFitMinP
                            && minPresent > bestFitPresent)
                        || (peak == bestFitPeak && idealDist == bestFitIdealDist && minP == bestFitMinP
                            && minPresent == bestFitPresent && i < bestFitIndex);
                }

                if (better)
                {
                    bestFit = slot;
                    bestFitPeak = peak;
                    bestFitMinP = minP;
                    bestFitPresent = minPresent;
                    bestFitIndex = i;
                    bestFitIdealDist = idealDist;
                }
            }
            else
            {
                // Soft-cap : au plus MaxOnBreakAt+1 (peak 7 @39 → P≈82 %) — refuse peak 8+
                var underSoftCap = true;
                var durFb = durationMinutes > 0 ? durationMinutes : BreakSlotPlanner.BreakDurationMinutes;
                for (var t = slot; t < slot.AddMinutes(durFb); t = t.AddMinutes(SampleStepMinutes))
                {
                    var present = PresentAt(ranges, t);
                    if (present <= 0) continue;
                    var softMax = MaxOnBreakAt(present, minPresencePercent) + 1;
                    if (CountOnBreakAt(already, t) + 1 > softMax)
                    {
                        underSoftCap = false;
                        break;
                    }
                }
                if (!underSoftCap) continue;

                bool betterFb = peak < bestFallbackPeak
                    || (peak == bestFallbackPeak && idealDist < bestFallbackIdealDist)
                    || (peak == bestFallbackPeak && idealDist == bestFallbackIdealDist && minP > bestFallbackMinP)
                    || (peak == bestFallbackPeak && idealDist == bestFallbackIdealDist
                        && minP == bestFallbackMinP && i < bestFallbackIndex);
                if (betterFb)
                {
                    bestFallback = slot;
                    bestFallbackMinP = minP;
                    bestFallbackPeak = peak;
                    bestFallbackPresent = minPresent;
                    bestFallbackIndex = i;
                    bestFallbackIdealDist = idealDist;
                }
            }
        }

        if (bestFit != null)
        {
            respectedThreshold = true;
            return bestFit;
        }

        if (bestFallback != null)
        {
            respectedThreshold = false;
            return bestFallback;
        }

        // Hard : min pic mais jamais au-delà du soft-cap (évite 11h×9)
        respectedThreshold = false;
        TimeOnly? hard = null;
        var hardPeak = int.MaxValue;
        var hardIndex = int.MaxValue;
        for (var i = 0; i < candidates.Count; i++)
        {
            var slot = candidates[i];
            var peak = PeakOnBreakIfAssigned(already, slot, durationMinutes);
            var underSoft = true;
            var durH = durationMinutes > 0 ? durationMinutes : BreakSlotPlanner.BreakDurationMinutes;
            for (var t = slot; t < slot.AddMinutes(durH); t = t.AddMinutes(SampleStepMinutes))
            {
                var present = PresentAt(ranges, t);
                if (present <= 0) continue;
                if (CountOnBreakAt(already, t) + 1 > MaxOnBreakAt(present, minPresencePercent) + 1)
                {
                    underSoft = false;
                    break;
                }
            }
            if (!underSoft) continue;
            if (peak < hardPeak || (peak == hardPeak && i < hardIndex))
            {
                hardPeak = peak;
                hardIndex = i;
                hard = slot;
            }
        }
        return hard ?? bestFallback;
    }

    /// <summary>
    /// Min P sur toute la journée (timeline) pour un jeu de pauses déjà assignées.
    /// </summary>
    public static decimal DayMinAvailabilityPercent(
        IReadOnlyList<ShiftRange> ranges,
        IReadOnlyList<BreakPlacement> breaks)
    {
        if (ranges.Count == 0) return 100m;

        var windowStart = ranges.Min(r => r.Start);
        var windowEnd = ranges.Max(r => r.End);
        if (windowStart >= windowEnd) return 100m;

        decimal minPct = 100m;
        var any = false;
        var t = new TimeOnly(windowStart.Hour, (windowStart.Minute / SampleStepMinutes) * SampleStepMinutes);
        while (t < windowEnd)
        {
            var present = PresentAt(ranges, t);
            if (present > 0)
            {
                any = true;
                var onBreak = CountOnBreakAt(breaks, t);
                var available = Math.Max(0, present - onBreak);
                var pct = Math.Round((decimal)available / present * 100, 1);
                if (pct < minPct) minPct = pct;
            }
            t = t.AddMinutes(SampleStepMinutes);
        }
        return any ? minPct : 100m;
    }

    public static bool DayRespectsPresence(
        IReadOnlyList<ShiftRange> ranges,
        IReadOnlyList<BreakPlacement> breaks,
        int minPresencePercent)
    {
        var target = BreakSlotPlanner.ClampMinPresence(minPresencePercent);
        if (target <= 0) return true;
        return DayMinAvailabilityPercent(ranges, breaks) + 0.05m >= target;
    }

    public static void AssignDayBreaks(
        List<ShiftAssignment> dayAssignments,
        IReadOnlyDictionary<int, SubServiceShiftConfig> configsById,
        int minPresencePercent,
        BreakFairnessCounters? fairnessCounters = null,
        IReadOnlySet<int>? specialCaseUserIds = null)
    {
        if (dayAssignments.Count == 0) return;
        fairnessCounters ??= new BreakFairnessCounters();
        specialCaseUserIds ??= new HashSet<int>();

        var packable = dayAssignments
            .Where(a => a.SubServiceShiftConfigId != null
                        && configsById.ContainsKey(a.SubServiceShiftConfigId.Value))
            .ToList();

        var ranges = new List<ShiftRange>(packable.Count);
        foreach (var a in packable)
        {
            var cfg = configsById[a.SubServiceShiftConfigId!.Value];
            ranges.Add(new ShiftRange(cfg.StartTime, cfg.EndTime));
        }

        var openingStart = packable
            .Select(a => configsById[a.SubServiceShiftConfigId!.Value].StartTime)
            .DefaultIfEmpty()
            .Min();

        var isCriticalDay = packable.Any(a => configsById[a.SubServiceShiftConfigId!.Value].IsCriticalCell);

        if (isCriticalDay)
        {
            RepackCriticalHourWaterFill(
                packable, configsById, ranges, minPresencePercent, openingStart, fairnessCounters,
                specialCaseUserIds);
            foreach (var a in packable.Where(x => x.BreakTime.HasValue))
            {
                var cfg = configsById[a.SubServiceShiftConfigId!.Value];
                fairnessCounters.Record(a.UserId, cfg.StartTime, a.BreakTime!.Value);
            }
            // PreferIdeal après waterfill peut ré-empiler midi — skip si déjà sous 83 %
            var placedNow = BuildPlacements(packable, configsById);
            if (DayMinAvailabilityPercent(ranges, placedNow) + 0.05m < 83m)
                PreferIdealPlus4WhenPossible(packable, configsById, ranges, minPresencePercent);
            EnforcePeakCap(packable, configsById, ranges, minPresencePercent);
            return;
        }

        var placed = new List<BreakPlacement>();
        var ordered = packable
            .OrderBy(a => configsById[a.SubServiceShiftConfigId!.Value].StartTime)
            .ThenBy(a => a.UserId)
            .ToList();
        foreach (var assignment in ordered)
        {
            var config = configsById[assignment.SubServiceShiftConfigId!.Value];
            var duration = config.BreakDurationMinutes > 0
                ? config.BreakDurationMinutes
                : BreakSlotPlanner.BreakDurationMinutes;
            var excludeExtremes = specialCaseUserIds.Contains(assignment.UserId);
            var candidates = BreakSlotPlanner.PackingCandidates(
                config.StartTime, false, excludeExtremes);
            var ideal = config.StartTime.AddHours(4);
            var chosen = PickBreakSlot(
                candidates, ranges, placed, duration, minPresencePercent,
                out _, preferEarliest: false, idealBreakStart: ideal)
                ?? ideal;
            assignment.BreakTime = chosen;
            placed.Add(new BreakPlacement(chosen, duration));
            fairnessCounters.Record(assignment.UserId, config.StartTime, chosen);
        }

        RepairToPresenceTarget(packable, configsById, ranges, minPresencePercent);
        EnforcePeakCap(packable, configsById, ranges, minPresencePercent);
    }

    /// <summary>
    /// Repack critique type superviseur : créneaux :00/:30 uniquement, jamais &gt; MaxOnBreakAt.
    /// Opening : 11h puis 12h puis 13h ; autres : +5h d'abord.
    /// </summary>
    private static void RepackCriticalHourWaterFill(
        List<ShiftAssignment> packable,
        IReadOnlyDictionary<int, SubServiceShiftConfig> configsById,
        IReadOnlyList<ShiftRange> ranges,
        int minPresencePercent,
        TimeOnly openingStart,
        BreakFairnessCounters fairnessCounters,
        IReadOnlySet<int> specialCaseUserIds)
    {
        var placed = new List<BreakPlacement>();
        foreach (var a in packable)
            a.BreakTime = null;

        var opening = packable
            .Where(a => configsById[a.SubServiceShiftConfigId!.Value].StartTime == openingStart)
            .OrderBy(a => fairnessCounters.EarlyCounts.GetValueOrDefault(a.UserId))
            .ThenBy(a => a.UserId)
            .ToList();
        var later = packable
            .Where(a => configsById[a.SubServiceShiftConfigId!.Value].StartTime != openingStart)
            .OrderBy(a => fairnessCounters.LateCounts.GetValueOrDefault(a.UserId))
            .ThenBy(a => configsById[a.SubServiceShiftConfigId!.Value].StartTime)
            .ThenBy(a => a.UserId)
            .ToList();

        TimeOnly? PickStrict(ShiftAssignment assignment, IReadOnlyList<TimeOnly> order)
        {
            var config = configsById[assignment.SubServiceShiftConfigId!.Value];
            var duration = config.BreakDurationMinutes > 0
                ? config.BreakDurationMinutes
                : BreakSlotPlanner.BreakDurationMinutes;
            var filtered = specialCaseUserIds.Contains(assignment.UserId)
                ? BreakSlotPlanner.WithoutExtremeCaseBreaks(config.StartTime, order)
                : order.ToList();
            foreach (var s in filtered)
            {
                if (FitsPresence(ranges, placed, s, duration, minPresencePercent))
                    return s;
            }
            return null;
        }

        void Commit(ShiftAssignment assignment, TimeOnly slot)
        {
            var config = configsById[assignment.SubServiceShiftConfigId!.Value];
            var duration = config.BreakDurationMinutes > 0
                ? config.BreakDurationMinutes
                : BreakSlotPlanner.BreakDurationMinutes;
            assignment.BreakTime = slot;
            placed.Add(new BreakPlacement(slot, duration));
        }

        // 1) Opening : max strict à 11h / 11h30 (FitsPresence only) — cas particulier : skip +3h
        foreach (var a in opening)
        {
            var cfg = configsById[a.SubServiceShiftConfigId!.Value];
            var duration = cfg.BreakDurationMinutes > 0 ? cfg.BreakDurationMinutes : 60;
            var openingSlots = specialCaseUserIds.Contains(a.UserId)
                ? new[] { cfg.StartTime.AddHours(3.5), cfg.StartTime.AddHours(4) }
                : new[] { cfg.StartTime.AddHours(3), cfg.StartTime.AddHours(3.5) };
            foreach (var s in openingSlots)
            {
                if (BreakSlotPlanner.IsExtremeCaseBreak(cfg.StartTime, s)
                    && specialCaseUserIds.Contains(a.UserId))
                    continue;
                if (!FitsPresence(ranges, placed, s, duration, minPresencePercent))
                    continue;
                Commit(a, s);
                break;
            }
        }

        // 2) Autres : +5h d'abord (plafond métier), puis +4h… dans WindowStart…WindowEnd
        foreach (var a in later)
        {
            var cfg = configsById[a.SubServiceShiftConfigId!.Value];
            var order = BreakSlotPlanner.PackingCandidatesSpread(
                cfg.StartTime, true);
            if (specialCaseUserIds.Contains(a.UserId))
                order = BreakSlotPlanner.WithoutExtremeCaseBreaks(cfg.StartTime, order);
            var slot = PickStrict(a, order);
            if (slot != null) Commit(a, slot.Value);
        }

        // 3) Reste Opening : 12h → 13h (max +5h = 13:00 pour 08:00)
        foreach (var a in opening.Where(x => !x.BreakTime.HasValue))
        {
            var cfg = configsById[a.SubServiceShiftConfigId!.Value];
            var order = new List<TimeOnly>
            {
                cfg.StartTime.AddHours(4),
                cfg.StartTime.AddHours(4.5),
                cfg.StartTime.AddHours(5)
            };
            foreach (var s in BreakSlotPlanner.DenseWindowSlots(
                         cfg.StartTime, true, 30, isOpeningShift: true))
            {
                if (!order.Contains(s) && s >= cfg.StartTime.AddHours(4))
                    order.Add(s);
            }
            if (specialCaseUserIds.Contains(a.UserId))
                order = BreakSlotPlanner.WithoutExtremeCaseBreaks(cfg.StartTime, order);
            var slot = PickStrict(a, order);
            if (slot != null) Commit(a, slot.Value);
        }

        // 4) Non assignés : fenêtre +3h…+5h, équité Early/Late, pas de créneau hors plafond
        var remaining = packable
            .Where(x => !x.BreakTime.HasValue)
            .OrderBy(a =>
            {
                var cfg = configsById[a.SubServiceShiftConfigId!.Value];
                return BreakSlotPlanner.IsOpeningShift(cfg.StartTime)
                    ? fairnessCounters.EarlyCounts.GetValueOrDefault(a.UserId)
                    : fairnessCounters.LateCounts.GetValueOrDefault(a.UserId);
            })
            .ThenBy(a => a.UserId)
            .ToList();

        foreach (var a in remaining)
        {
            var cfg = configsById[a.SubServiceShiftConfigId!.Value];
            var duration = cfg.BreakDurationMinutes > 0 ? cfg.BreakDurationMinutes : 60;
            var order = BreakSlotPlanner.DenseWindowSlots(
                cfg.StartTime, true, 30,
                cfg.StartTime == openingStart || BreakSlotPlanner.IsOpeningShift(cfg.StartTime));

            if (specialCaseUserIds.Contains(a.UserId))
                order = BreakSlotPlanner.WithoutExtremeCaseBreaks(cfg.StartTime, order);

            var preferLate = BreakSlotPlanner.IsOpeningShift(cfg.StartTime)
                && fairnessCounters.EarlyCounts.GetValueOrDefault(a.UserId) > 0
                && !specialCaseUserIds.Contains(a.UserId);
            if (preferLate)
                order = order.OrderByDescending(s => s).ToList();

            var slot = PickStrict(a, order);
            if (slot == null)
            {
                TimeOnly? best = null;
                var bestPeak = int.MaxValue;
                foreach (var s in order)
                {
                    var peak = PeakOnBreakIfAssigned(placed, s, duration);
                    if (peak < bestPeak)
                    {
                        bestPeak = peak;
                        best = s;
                    }
                }
                slot = best ?? cfg.StartTime.AddHours(4);
            }
            Commit(a, slot.Value);
        }
    }

    /// <summary>
    /// Améliore le P vers un plancher (ex. 83 %) avec soft-cap maxBreak+1.
    /// </summary>
    private static void RepairTowardFloor(
        List<ShiftAssignment> packable,
        IReadOnlyDictionary<int, SubServiceShiftConfig> configsById,
        IReadOnlyList<ShiftRange> ranges,
        int minPresencePercent,
        decimal floorPercent)
    {
        var openingStart = packable
            .Select(a => configsById[a.SubServiceShiftConfigId!.Value].StartTime)
            .DefaultIfEmpty()
            .Min();

        for (var pass = 0; pass < 80; pass++)
        {
            var placed = BuildPlacements(packable, configsById);
            var minP = DayMinAvailabilityPercent(ranges, placed);
            if (minP + 0.05m >= floorPercent)
                return;

            var improved = false;
            foreach (var assignment in packable)
            {
                var config = configsById[assignment.SubServiceShiftConfigId!.Value];
                var duration = config.BreakDurationMinutes > 0
                    ? config.BreakDurationMinutes
                    : BreakSlotPlanner.BreakDurationMinutes;
                var isOpening = config.StartTime == openingStart;
                var candidates = isOpening
                    ? BreakSlotPlanner.PackingCandidatesEarlyWave(config.StartTime, config.IsCriticalCell)
                    : BreakSlotPlanner.PackingCandidatesSpread(config.StartTime, config.IsCriticalCell);

                var others = packable
                    .Where(a => !ReferenceEquals(a, assignment) && a.BreakTime.HasValue)
                    .Select(a =>
                    {
                        var c = configsById[a.SubServiceShiftConfigId!.Value];
                        var d = c.BreakDurationMinutes > 0
                            ? c.BreakDurationMinutes
                            : BreakSlotPlanner.BreakDurationMinutes;
                        return new BreakPlacement(a.BreakTime!.Value, d);
                    })
                    .ToList();

                var bestSlot = assignment.BreakTime;
                var bestP = minP;
                foreach (var slot in candidates)
                {
                    var softOk = true;
                    for (var t = slot; t < slot.AddMinutes(duration); t = t.AddMinutes(SampleStepMinutes))
                    {
                        var present = PresentAt(ranges, t);
                        if (present <= 0) continue;
                        if (CountOnBreakAt(others, t) + 1 > MaxOnBreakAt(present, minPresencePercent) + 1)
                        {
                            softOk = false;
                            break;
                        }
                    }
                    if (!softOk) continue;

                    var trial = others.Append(new BreakPlacement(slot, duration)).ToList();
                    var trialP = DayMinAvailabilityPercent(ranges, trial);
                    if (trialP > bestP + 0.05m)
                    {
                        bestP = trialP;
                        bestSlot = slot;
                    }
                }

                if (bestSlot.HasValue && assignment.BreakTime != bestSlot && bestP > minP + 0.05m)
                {
                    assignment.BreakTime = bestSlot;
                    improved = true;
                    break;
                }
            }

            if (!improved) return;
        }
    }

    /// <summary>
    /// Cellule critique : si une pause +3h/+5h peut passer en +4h/+4h30 sans casser P, on le fait.
    /// </summary>
    private static void PreferIdealPlus4WhenPossible(
        List<ShiftAssignment> packable,
        IReadOnlyDictionary<int, SubServiceShiftConfig> configsById,
        IReadOnlyList<ShiftRange> ranges,
        int minPresencePercent)
    {
        foreach (var assignment in packable)
        {
            var config = configsById[assignment.SubServiceShiftConfigId!.Value];
            if (!config.IsCriticalCell || !assignment.BreakTime.HasValue)
                continue;

            var duration = config.BreakDurationMinutes > 0
                ? config.BreakDurationMinutes
                : BreakSlotPlanner.BreakDurationMinutes;
            var current = assignment.BreakTime.Value;
            var ideal = config.StartTime.AddHours(4);
            var idealAlt = config.StartTime.AddHours(4.5);
            if (current == ideal || current == idealAlt)
                continue;

            // Uniquement depuis créneaux de phase (+3h/+5h/…) vers +4h
            if (!BreakSlotPlanner.IsExtremeCaseBreak(config.StartTime, current))
                continue;

            var others = packable
                .Where(a => !ReferenceEquals(a, assignment) && a.BreakTime.HasValue)
                .Select(a =>
                {
                    var c = configsById[a.SubServiceShiftConfigId!.Value];
                    var d = c.BreakDurationMinutes > 0
                        ? c.BreakDurationMinutes
                        : BreakSlotPlanner.BreakDurationMinutes;
                    return new BreakPlacement(a.BreakTime!.Value, d);
                })
                .ToList();

            foreach (var slot in new[] { ideal, idealAlt })
            {
                if (!FitsPresence(ranges, others, slot, duration, minPresencePercent))
                    continue;
                assignment.BreakTime = slot;
                break;
            }
        }
    }

    /// <summary>
    /// Déplace des pauses hors des instants où onBreak &gt; MaxOnBreakAt(present).
    /// </summary>
    private static void EnforcePeakCap(
        List<ShiftAssignment> packable,
        IReadOnlyDictionary<int, SubServiceShiftConfig> configsById,
        IReadOnlyList<ShiftRange> ranges,
        int minPresencePercent)
    {
        var openingStart = packable
            .Select(a => configsById[a.SubServiceShiftConfigId!.Value].StartTime)
            .DefaultIfEmpty()
            .Min();

        for (var pass = 0; pass < 200; pass++)
        {
            var placed = BuildPlacements(packable, configsById);
            if (DayRespectsPresence(ranges, placed, minPresencePercent))
                return;

            TimeOnly? overloadInstant = null;
            var overloadExtra = 0;
            if (ranges.Count > 0)
            {
                var windowStart = ranges.Min(r => r.Start);
                var windowEnd = ranges.Max(r => r.End);
                var t = new TimeOnly(windowStart.Hour, (windowStart.Minute / SampleStepMinutes) * SampleStepMinutes);
                while (t < windowEnd)
                {
                    var present = PresentAt(ranges, t);
                    if (present > 0)
                    {
                        var maxBreak = MaxOnBreakAt(present, minPresencePercent);
                        var onBreak = CountOnBreakAt(placed, t);
                        var extra = onBreak - maxBreak;
                        if (extra > overloadExtra)
                        {
                            overloadExtra = extra;
                            overloadInstant = t;
                        }
                    }
                    t = t.AddMinutes(SampleStepMinutes);
                }
            }

            if (overloadInstant == null || overloadExtra <= 0)
                return;

            var instant = overloadInstant.Value;
            var movers = packable
                .Where(a =>
                {
                    if (!a.BreakTime.HasValue) return false;
                    var c = configsById[a.SubServiceShiftConfigId!.Value];
                    var d = c.BreakDurationMinutes > 0 ? c.BreakDurationMinutes : BreakSlotPlanner.BreakDurationMinutes;
                    return a.BreakTime.Value <= instant && a.BreakTime.Value.AddMinutes(d) > instant;
                })
                .OrderByDescending(a => a.UserId)
                .ToList();

            var moved = false;
            foreach (var assignment in movers)
            {
                var config = configsById[assignment.SubServiceShiftConfigId!.Value];
                var duration = config.BreakDurationMinutes > 0
                    ? config.BreakDurationMinutes
                    : BreakSlotPlanner.BreakDurationMinutes;
                var isOpening = config.StartTime == openingStart;
                var candidates = isOpening
                    ? BreakSlotPlanner.PackingCandidatesEarlyWave(
                        config.StartTime, config.IsCriticalCell)
                    : BreakSlotPlanner.PackingCandidatesSpread(
                        config.StartTime, config.IsCriticalCell);

                var others = packable
                    .Where(a => !ReferenceEquals(a, assignment) && a.BreakTime.HasValue)
                    .Select(a =>
                    {
                        var c = configsById[a.SubServiceShiftConfigId!.Value];
                        var d = c.BreakDurationMinutes > 0
                            ? c.BreakDurationMinutes
                            : BreakSlotPlanner.BreakDurationMinutes;
                        return new BreakPlacement(a.BreakTime!.Value, d);
                    })
                    .ToList();

                TimeOnly? best = null;
                var bestPeak = int.MaxValue;
                var bestP = -1m;
                foreach (var slot in candidates)
                {
                    if (slot == assignment.BreakTime) continue;
                    if (!FitsPresence(ranges, others, slot, duration, minPresencePercent))
                        continue;
                    var trial = others.Append(new BreakPlacement(slot, duration)).ToList();
                    var peak = 0;
                    for (var t = slot; t < slot.AddMinutes(duration); t = t.AddMinutes(SampleStepMinutes))
                    {
                        var n = CountOnBreakAt(trial, t);
                        if (n > peak) peak = n;
                    }
                    var trialP = DayMinAvailabilityPercent(ranges, trial);
                    if (peak < bestPeak || (peak == bestPeak && trialP > bestP))
                    {
                        bestPeak = peak;
                        bestP = trialP;
                        best = slot;
                    }
                }

                if (best != null)
                {
                    assignment.BreakTime = best;
                    moved = true;
                    break;
                }
            }

            if (!moved)
                return;
        }
    }

    /// <summary>
    /// Déplace des pauses vers des créneaux faisables tant que le min P &lt; cible.
    /// </summary>
    private static void RepairToPresenceTarget(
        List<ShiftAssignment> packable,
        IReadOnlyDictionary<int, SubServiceShiftConfig> configsById,
        IReadOnlyList<ShiftRange> ranges,
        int minPresencePercent)
    {
        var target = BreakSlotPlanner.ClampMinPresence(minPresencePercent);
        if (target <= 0) return;
        var openingStart = packable
            .Select(a => configsById[a.SubServiceShiftConfigId!.Value].StartTime)
            .DefaultIfEmpty()
            .Min();

        for (var pass = 0; pass < 120; pass++)
        {
            var placed = BuildPlacements(packable, configsById);
            var minP = DayMinAvailabilityPercent(ranges, placed);
            if (minP + 0.05m >= target)
                return;

            var improved = false;
            foreach (var assignment in packable)
            {
                var config = configsById[assignment.SubServiceShiftConfigId!.Value];
                var duration = config.BreakDurationMinutes > 0
                    ? config.BreakDurationMinutes
                    : BreakSlotPlanner.BreakDurationMinutes;
                var isOpening = config.StartTime == openingStart;
                var candidates = isOpening
                    ? BreakSlotPlanner.PackingCandidatesEarlyWave(
                        config.StartTime, config.IsCriticalCell)
                    : BreakSlotPlanner.PackingCandidatesSpread(
                        config.StartTime, config.IsCriticalCell);

                var others = packable
                    .Where(a => !ReferenceEquals(a, assignment) && a.BreakTime.HasValue)
                    .Select(a =>
                    {
                        var c = configsById[a.SubServiceShiftConfigId!.Value];
                        var d = c.BreakDurationMinutes > 0
                            ? c.BreakDurationMinutes
                            : BreakSlotPlanner.BreakDurationMinutes;
                        return new BreakPlacement(a.BreakTime!.Value, d);
                    })
                    .ToList();

                var bestSlot = assignment.BreakTime;
                var bestP = minP;
                // 1) Déplacements qui respectent le seuil
                foreach (var slot in candidates)
                {
                    if (!FitsPresence(ranges, others, slot, duration, minPresencePercent))
                        continue;
                    var trial = others.Append(new BreakPlacement(slot, duration)).ToList();
                    var trialP = DayMinAvailabilityPercent(ranges, trial);
                    if (trialP > bestP + 0.05m)
                    {
                        bestP = trialP;
                        bestSlot = slot;
                    }
                }

                // 2) Sinon tout créneau qui améliore le P journée (désempilement)
                if (bestP <= minP + 0.05m)
                {
                    foreach (var slot in candidates)
                    {
                        var trial = others.Append(new BreakPlacement(slot, duration)).ToList();
                        var trialP = DayMinAvailabilityPercent(ranges, trial);
                        if (trialP > bestP + 0.05m)
                        {
                            bestP = trialP;
                            bestSlot = slot;
                        }
                    }
                }

                if (bestSlot.HasValue
                    && assignment.BreakTime != bestSlot
                    && bestP > minP + 0.05m)
                {
                    assignment.BreakTime = bestSlot;
                    improved = true;
                    break; // recommencer une passe avec le nouvel état
                }
            }

            if (!improved)
                return;
        }
    }

    private static List<BreakPlacement> BuildPlacements(
        List<ShiftAssignment> packable,
        IReadOnlyDictionary<int, SubServiceShiftConfig> configsById)
    {
        var list = new List<BreakPlacement>();
        foreach (var a in packable)
        {
            if (!a.BreakTime.HasValue) continue;
            var c = configsById[a.SubServiceShiftConfigId!.Value];
            var d = c.BreakDurationMinutes > 0
                ? c.BreakDurationMinutes
                : BreakSlotPlanner.BreakDurationMinutes;
            list.Add(new BreakPlacement(a.BreakTime.Value, d));
        }
        return list;
    }
}
