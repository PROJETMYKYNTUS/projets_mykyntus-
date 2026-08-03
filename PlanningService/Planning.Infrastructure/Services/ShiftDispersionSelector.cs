using Planning.Domain.Entities;

namespace Planning.Infrastructure.Services;

/// <summary>
/// Dispersion + équité : max 2× le même shift / semaine, pas de consécutif,
/// répartition équilibrée (priorité même niveau).
/// </summary>
public static class ShiftDispersionSelector
{
    public const int MaxSameShiftPerWeek = 2;
    private const int FairnessRepairMaxPasses = 40;

    public sealed class SelectionResult
    {
        public required SubServiceShiftConfig Shift { get; init; }
        public bool SoftConsecutiveAllowed { get; init; }
        public bool SoftQuotaExceeded { get; init; }

        public bool HasSoftViolation => SoftConsecutiveAllowed || SoftQuotaExceeded;
    }

    public static List<SubServiceShiftConfig> PreferredOrder(
        IReadOnlyList<SubServiceShiftConfig> orderedShifts,
        int startIdx,
        int dayIdx)
    {
        var n = orderedShifts.Count;
        if (n == 0) return new List<SubServiceShiftConfig>();

        var result = new List<SubServiceShiftConfig>(n);
        for (var i = 0; i < n; i++)
            result.Add(orderedShifts[(startIdx + dayIdx + i) % n]);
        return result;
    }

    public static int CountThisWeek(
        IReadOnlyDictionary<int, List<int>> weekAssignmentsByUser,
        int userId,
        int shiftConfigId)
    {
        if (!weekAssignmentsByUser.TryGetValue(userId, out var list) || list.Count == 0)
            return 0;
        return list.Count(id => id == shiftConfigId);
    }

    public static int? YesterdayShiftId(
        IReadOnlyDictionary<int, List<int>> weekAssignmentsByUser,
        int userId)
    {
        if (!weekAssignmentsByUser.TryGetValue(userId, out var list) || list.Count == 0)
            return null;
        return list[^1];
    }

    private static int EffectiveMax(int shiftCount) =>
        shiftCount <= 1 ? int.MaxValue : MaxSameShiftPerWeek;

    /// <summary>
    /// Moyenne des occurrences d'un shift parmi les users du même Level (hors user courant).
    /// </summary>
    private static double PeerAverageForShift(
        int shiftConfigId,
        int userId,
        int userLevel,
        IReadOnlyDictionary<int, List<int>> weekHistory,
        IReadOnlyDictionary<int, User>? usersById)
    {
        if (usersById == null || usersById.Count == 0)
            return 0;

        var peers = usersById.Values
            .Where(u => u.Id != userId && u.Level == userLevel)
            .ToList();
        if (peers.Count == 0)
        {
            peers = usersById.Values.Where(u => u.Id != userId).ToList();
            if (peers.Count == 0) return 0;
        }

        return peers.Average(u => CountThisWeek(weekHistory, u.Id, shiftConfigId));
    }

    /// <summary>
    /// Choisit un shift : remplit d'abord les quotas prod (RequiredCount),
    /// puis max2 / non-consécutif / équité. Ne dépasse un quota que si
    /// tous les sièges requis sont déjà pourvus (surplus d'effectif).
    /// </summary>
    public static SelectionResult Select(
        IReadOnlyList<SubServiceShiftConfig> orderedShifts,
        int startIdx,
        int dayIdx,
        int userId,
        IReadOnlyDictionary<int, List<int>> weekAssignmentsByUser,
        IReadOnlyDictionary<int, int> shiftCountToday,
        IReadOnlyDictionary<int, User>? usersById = null)
    {
        var preferred = PreferredOrder(orderedShifts, startIdx, dayIdx);
        if (preferred.Count == 0)
            throw new InvalidOperationException("Aucun shift configuré.");

        var maxSame = EffectiveMax(orderedShifts.Count);
        var yesterdayId = YesterdayShiftId(weekAssignmentsByUser, userId);
        var userLevel = usersById != null && usersById.TryGetValue(userId, out var me)
            ? me.Level
            : 0;

        bool UnderQuota(SubServiceShiftConfig s) =>
            s.RequiredCount <= 0
            || shiftCountToday.GetValueOrDefault(s.Id, 0) < s.RequiredCount;

        bool UnderMax(SubServiceShiftConfig s) =>
            CountThisWeek(weekAssignmentsByUser, userId, s.Id) < maxSame;

        SubServiceShiftConfig RankPick(IEnumerable<SubServiceShiftConfig> candidates) =>
            candidates
                .OrderBy(s => CountThisWeek(weekAssignmentsByUser, userId, s.Id))
                .ThenBy(s => CountThisWeek(weekAssignmentsByUser, userId, s.Id)
                             - PeerAverageForShift(s.Id, userId, userLevel, weekAssignmentsByUser, usersById))
                .ThenBy(s => preferred.IndexOf(s))
                .First();

        // 1) Strict : siège libre + non-consécutif + max2 + équité
        var strict = preferred.Where(s =>
            UnderQuota(s) && s.Id != yesterdayId && UnderMax(s)).ToList();
        if (strict.Count > 0)
            return new SelectionResult { Shift = RankPick(strict) };

        // 2) Siège libre + max2 (autorise consécutif)
        var underQuotaMax = preferred.Where(s => UnderQuota(s) && UnderMax(s)).ToList();
        if (underQuotaMax.Count > 0)
            return new SelectionResult
            {
                Shift = RankPick(underQuotaMax),
                SoftConsecutiveAllowed = true
            };

        // 3) Siège libre prioritaire sur max2 (besoin prod > dispersion)
        var underQuotaAny = preferred.Where(UnderQuota).ToList();
        if (underQuotaAny.Count > 0)
            return new SelectionResult
            {
                Shift = RankPick(underQuotaAny),
                SoftConsecutiveAllowed = true,
                SoftQuotaExceeded = false
            };

        // 4) Tous les quotas sont remplis → surplus d'effectif (autorise dépassement sous max2)
        var underCap = preferred.Where(UnderMax).ToList();
        if (underCap.Count > 0)
        {
            var chosen = RankPick(underCap);
            return new SelectionResult
            {
                Shift = chosen,
                SoftQuotaExceeded = true,
                SoftConsecutiveAllowed = chosen.Id == yesterdayId
            };
        }

        // 5) Un seul shift possible
        return new SelectionResult
        {
            Shift = RankPick(preferred),
            SoftQuotaExceeded = true,
            SoftConsecutiveAllowed = true
        };
    }

    public static SubServiceShiftConfig SelectSaturday(
        IReadOnlyList<SubServiceShiftConfig> orderedByStart,
        int preferredIndex,
        int? fridayShiftId,
        IReadOnlyDictionary<int, List<int>> weekAssignmentsByUser,
        int userId,
        IReadOnlyDictionary<int, User>? usersById = null,
        IReadOnlyDictionary<int, int>? shiftCountToday = null)
    {
        if (orderedByStart.Count == 0)
            throw new InvalidOperationException("Aucun shift configuré.");

        var maxSame = EffectiveMax(orderedByStart.Count);
        var preferred = new List<SubServiceShiftConfig>(orderedByStart.Count);
        for (var i = 0; i < orderedByStart.Count; i++)
            preferred.Add(orderedByStart[(preferredIndex + i) % orderedByStart.Count]);

        var userLevel = usersById != null && usersById.TryGetValue(userId, out var me)
            ? me.Level
            : 0;
        shiftCountToday ??= new Dictionary<int, int>();

        bool UnderQuota(SubServiceShiftConfig s) =>
            s.RequiredCount <= 0
            || shiftCountToday.GetValueOrDefault(s.Id, 0) < s.RequiredCount;

        bool UnderMax(SubServiceShiftConfig s) =>
            CountThisWeek(weekAssignmentsByUser, userId, s.Id) < maxSame;

        SubServiceShiftConfig RankPick(IEnumerable<SubServiceShiftConfig> candidates) =>
            candidates
                .OrderBy(s => CountThisWeek(weekAssignmentsByUser, userId, s.Id))
                .ThenBy(s => CountThisWeek(weekAssignmentsByUser, userId, s.Id)
                             - PeerAverageForShift(s.Id, userId, userLevel, weekAssignmentsByUser, usersById))
                .ThenBy(s => preferred.IndexOf(s))
                .First();

        // 1) Strict : ≠ vendredi + max2 + siège libre + équité niveau
        var strict = preferred.Where(s =>
            s.Id != fridayShiftId && UnderMax(s) && UnderQuota(s)).ToList();
        if (strict.Count > 0)
            return RankPick(strict);

        // 2) Siège libre + max2 (autorise = vendredi)
        var underQuotaMax = preferred.Where(s => UnderQuota(s) && UnderMax(s)).ToList();
        if (underQuotaMax.Count > 0)
            return RankPick(underQuotaMax);

        // 3) Siège libre prioritaire (besoin prod)
        var underQuotaAny = preferred.Where(UnderQuota).ToList();
        if (underQuotaAny.Count > 0)
            return RankPick(underQuotaAny);

        // 4) Quotas remplis → surplus sous max2
        var underCap = preferred.Where(UnderMax).ToList();
        if (underCap.Count > 0)
            return RankPick(underCap);

        return RankPick(preferred);
    }

    /// <summary>
    /// Répare dispersion Lun–Sam (présents uniquement). Ne crée pas d’assignation samedi.
    /// </summary>
    public static void RepairWeekdayDispersion(
        List<ShiftAssignment> assignments,
        List<SubServiceShiftConfig> shiftConfigs)
    {
        _ = shiftConfigs;
        var workDays = assignments
            .Where(a => !a.IsOnLeave && !a.IsHoliday && a.SubServiceShiftConfigId != null)
            .GroupBy(a => a.AssignedDate)
            .OrderBy(g => g.Key)
            .ToList();

        if (workDays.Count == 0) return;

        for (var dayIndex = 0; dayIndex < workDays.Count; dayIndex++)
        {
            var dayGroup = workDays[dayIndex].ToList();
            var prevDate = dayIndex > 0 ? workDays[dayIndex - 1].Key : (DateOnly?)null;

            foreach (var a in dayGroup)
            {
                if (a.IsManagerOverride) continue;
                if (!NeedsRepair(a, assignments, prevDate, shiftConfigs.Count))
                    continue;

                foreach (var b in dayGroup)
                {
                    if (b.UserId == a.UserId) continue;
                    if (b.IsManagerOverride) continue;
                    if (b.SubServiceShiftConfigId == a.SubServiceShiftConfigId) continue;
                    // Ne pas mélanger demi-journée débutant / journée pleine
                    if (a.IsHalfDaySaturday != b.IsHalfDaySaturday) continue;

                    if (SwapImprovesDispersion(a, b, assignments, prevDate, shiftConfigs.Count))
                    {
                        (a.SubServiceShiftConfigId, b.SubServiceShiftConfigId) =
                            (b.SubServiceShiftConfigId, a.SubServiceShiftConfigId);
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Swaps même niveau pour réduire les écarts (Lun–Sam, présents uniquement).
    /// </summary>
    public static void RepairFairness(
        List<ShiftAssignment> assignments,
        List<SubServiceShiftConfig> shiftConfigs,
        IReadOnlyDictionary<int, User> usersById)
    {
        if (shiftConfigs.Count <= 1) return;

        var workDays = assignments
            .Where(a => !a.IsOnLeave && !a.IsHoliday && a.SubServiceShiftConfigId != null)
            .GroupBy(a => a.AssignedDate)
            .OrderBy(g => g.Key)
            .ToList();

        if (workDays.Count == 0) return;

        for (var pass = 0; pass < FairnessRepairMaxPasses; pass++)
        {
            var improved = false;

            for (var dayIndex = 0; dayIndex < workDays.Count; dayIndex++)
            {
                var dayGroup = workDays[dayIndex].ToList();
                var prevDate = dayIndex > 0 ? workDays[dayIndex - 1].Key : (DateOnly?)null;
                var nextDate = dayIndex + 1 < workDays.Count ? workDays[dayIndex + 1].Key : (DateOnly?)null;

                for (var i = 0; i < dayGroup.Count; i++)
                {
                    for (var j = i + 1; j < dayGroup.Count; j++)
                    {
                        var a = dayGroup[i];
                        var b = dayGroup[j];
                        if (a.IsManagerOverride || b.IsManagerOverride) continue;
                        if (a.SubServiceShiftConfigId == b.SubServiceShiftConfigId) continue;
                        if (a.IsHalfDaySaturday != b.IsHalfDaySaturday) continue;

                        if (!usersById.TryGetValue(a.UserId, out var ua)
                            || !usersById.TryGetValue(b.UserId, out var ub)
                            || ua.Level != ub.Level)
                            continue;

                        if (!FairnessSwapAllowed(a, b, assignments, prevDate, nextDate, shiftConfigs.Count))
                            continue;

                        if (!FairnessSwapImproves(a, b, assignments, usersById, shiftConfigs))
                            continue;

                        (a.SubServiceShiftConfigId, b.SubServiceShiftConfigId) =
                            (b.SubServiceShiftConfigId, a.SubServiceShiftConfigId);
                        improved = true;
                    }
                }
            }

            if (!improved) break;
        }
    }

    private static bool FairnessSwapAllowed(
        ShiftAssignment a,
        ShiftAssignment b,
        List<ShiftAssignment> all,
        DateOnly? prevDate,
        DateOnly? nextDate,
        int shiftCount)
    {
        var maxSame = EffectiveMax(shiftCount);
        var aNew = b.SubServiceShiftConfigId!.Value;
        var bNew = a.SubServiceShiftConfigId!.Value;

        // Max 2 après swap (Lun–Sam travaillés)
        if (WorkedCount(all, a.UserId, aNew, exclude: a) + 1 > maxSame) return false;
        if (WorkedCount(all, b.UserId, bNew, exclude: b) + 1 > maxSame) return false;

        // Non-consécutif avec veille / lendemain
        if (AdjacentEquals(all, a.UserId, prevDate, aNew)) return false;
        if (AdjacentEquals(all, a.UserId, nextDate, aNew)) return false;
        if (AdjacentEquals(all, b.UserId, prevDate, bNew)) return false;
        if (AdjacentEquals(all, b.UserId, nextDate, bNew)) return false;

        return true;
    }

    private static bool AdjacentEquals(
        List<ShiftAssignment> all, int userId, DateOnly? date, int configId)
    {
        if (!date.HasValue) return false;
        var adj = all.FirstOrDefault(x =>
            x.UserId == userId
            && x.AssignedDate == date.Value
            && !x.IsOnLeave && !x.IsHoliday
            && x.SubServiceShiftConfigId != null);
        return adj?.SubServiceShiftConfigId == configId;
    }

    private static int WorkedCount(
        List<ShiftAssignment> all, int userId, int configId, ShiftAssignment? exclude = null) =>
        all.Count(x =>
            x.UserId == userId
            && !x.IsOnLeave && !x.IsHoliday
            && x.SubServiceShiftConfigId == configId
            && (exclude == null || !ReferenceEquals(x, exclude)));

    private static bool FairnessSwapImproves(
        ShiftAssignment a,
        ShiftAssignment b,
        List<ShiftAssignment> all,
        IReadOnlyDictionary<int, User> usersById,
        List<SubServiceShiftConfig> shiftConfigs)
    {
        var aOld = a.SubServiceShiftConfigId!.Value;
        var bOld = b.SubServiceShiftConfigId!.Value;
        var before = FairnessPenalty(all, usersById, shiftConfigs);
        // Simulate
        a.SubServiceShiftConfigId = bOld;
        b.SubServiceShiftConfigId = aOld;
        var after = FairnessPenalty(all, usersById, shiftConfigs);
        // Revert simulation (caller will re-apply if true)
        a.SubServiceShiftConfigId = aOld;
        b.SubServiceShiftConfigId = bOld;
        return after < before;
    }

    /// <summary>Somme des (max-min) par shift et par niveau.</summary>
    private static int FairnessPenalty(
        List<ShiftAssignment> all,
        IReadOnlyDictionary<int, User> usersById,
        List<SubServiceShiftConfig> shiftConfigs)
    {
        var penalty = 0;
        var workers = all
            .Where(x => !x.IsOnLeave && !x.IsHoliday && x.SubServiceShiftConfigId != null)
            .Select(x => x.UserId)
            .Distinct()
            .Where(id => usersById.ContainsKey(id))
            .ToList();

        foreach (var levelGroup in workers.GroupBy(id => usersById[id].Level))
        {
            var ids = levelGroup.ToList();
            if (ids.Count < 2) continue;

            foreach (var cfg in shiftConfigs)
            {
                var counts = ids
                    .Select(uid => WorkedCount(all, uid, cfg.Id))
                    .ToList();
                penalty += counts.Max() - counts.Min();
            }
        }

        return penalty;
    }

    private static bool NeedsRepair(
        ShiftAssignment a,
        List<ShiftAssignment> all,
        DateOnly? prevDate,
        int shiftCount)
    {
        var maxSame = EffectiveMax(shiftCount);
        var cfgId = a.SubServiceShiftConfigId!.Value;

        if (prevDate.HasValue)
        {
            var yesterday = all.FirstOrDefault(x =>
                x.UserId == a.UserId
                && x.AssignedDate == prevDate.Value
                && !x.IsOnLeave && !x.IsHoliday
                && x.SubServiceShiftConfigId != null);
            if (yesterday?.SubServiceShiftConfigId == cfgId)
                return true;
        }

        var count = all.Count(x =>
            x.UserId == a.UserId
            && !x.IsOnLeave && !x.IsHoliday
            && x.SubServiceShiftConfigId == cfgId
            && x.AssignedDate <= a.AssignedDate);

        return count > maxSame;
    }

    private static bool SwapImprovesDispersion(
        ShiftAssignment a,
        ShiftAssignment b,
        List<ShiftAssignment> all,
        DateOnly? prevDate,
        int shiftCount)
    {
        var aId = a.SubServiceShiftConfigId!.Value;
        var bId = b.SubServiceShiftConfigId!.Value;

        var aBadBefore = ViolationScore(a.UserId, aId, a.AssignedDate, all, prevDate, shiftCount);
        var bBadBefore = ViolationScore(b.UserId, bId, b.AssignedDate, all, prevDate, shiftCount);
        var aBadAfter = ViolationScore(a.UserId, bId, a.AssignedDate, all, prevDate, shiftCount, a, bId);
        var bBadAfter = ViolationScore(b.UserId, aId, b.AssignedDate, all, prevDate, shiftCount, b, aId);

        return (aBadAfter + bBadAfter) < (aBadBefore + bBadBefore);
    }

    private static int ViolationScore(
        int userId,
        int configId,
        DateOnly date,
        List<ShiftAssignment> all,
        DateOnly? prevDate,
        int shiftCount,
        ShiftAssignment? replaceAssignment = null,
        int? replaceWithConfigId = null)
    {
        var maxSame = EffectiveMax(shiftCount);
        var score = 0;
        var effectiveForDate = replaceAssignment != null && replaceWithConfigId.HasValue
            ? replaceWithConfigId.Value
            : configId;

        if (prevDate.HasValue)
        {
            var yesterday = all.FirstOrDefault(x =>
                x.UserId == userId
                && x.AssignedDate == prevDate.Value
                && !x.IsOnLeave && !x.IsHoliday
                && x.SubServiceShiftConfigId != null);
            if (yesterday?.SubServiceShiftConfigId == effectiveForDate)
                score += 2;
        }

        var count = 0;
        foreach (var x in all.Where(x =>
                     x.UserId == userId
                     && !x.IsOnLeave && !x.IsHoliday
                     && x.SubServiceShiftConfigId != null
                     && x.AssignedDate <= date))
        {
            var id = replaceAssignment != null
                     && ReferenceEquals(x, replaceAssignment)
                     && replaceWithConfigId.HasValue
                ? replaceWithConfigId.Value
                : x.SubServiceShiftConfigId!.Value;
            if (id == effectiveForDate)
                count++;
        }

        if (count > maxSame)
            score += (count - maxSame) * 3;

        return score;
    }

    public static List<string> BuildDispersionWarnings(
        List<ShiftAssignment> assignments,
        IReadOnlyDictionary<int, User> usersById,
        IReadOnlyDictionary<int, SubServiceShiftConfig>? configsById = null)
    {
        var warnings = new List<string>();
        var byUser = assignments
            .Where(a => !a.IsOnLeave && !a.IsHoliday && a.SubServiceShiftConfigId != null)
            .GroupBy(a => a.UserId);

        foreach (var g in byUser)
        {
            var name = usersById.TryGetValue(g.Key, out var u)
                ? $"{u.FirstName} {u.LastName}".Trim()
                : $"#{g.Key}";

            var ordered = g.OrderBy(a => a.AssignedDate).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].SubServiceShiftConfigId == ordered[i - 1].SubServiceShiftConfigId)
                {
                    warnings.Add(
                        $"{name} : même shift sur jours consécutifs ({ordered[i - 1].AssignedDate:dd/MM}–{ordered[i].AssignedDate:dd/MM})");
                    break;
                }
            }

            foreach (var byShift in ordered.GroupBy(a => a.SubServiceShiftConfigId!.Value))
            {
                if (byShift.Count() > MaxSameShiftPerWeek)
                {
                    var label = configsById != null && configsById.TryGetValue(byShift.Key, out var cfg)
                        ? cfg.Label
                        : byShift.First().SubServiceShiftConfig?.Label ?? byShift.Key.ToString();
                    warnings.Add(
                        $"{name} : shift « {label} » {byShift.Count()}× sur la semaine (max {MaxSameShiftPerWeek})");
                }
            }
        }

        // Écarts inter-employés même niveau
        if (configsById != null && configsById.Count > 0)
        {
            var workers = byUser.Select(g => g.Key).Where(usersById.ContainsKey).ToList();
            foreach (var levelGroup in workers.GroupBy(id => usersById[id].Level))
            {
                var ids = levelGroup.ToList();
                if (ids.Count < 2) continue;
                var levelLabel = levelGroup.Key switch
                {
                    1 => "Débutant",
                    2 => "Confirmé",
                    _ => levelGroup.Key >= 3 ? "Expert" : $"N{levelGroup.Key}"
                };

                foreach (var cfg in configsById.Values)
                {
                    var counts = ids
                        .Select(uid => (
                            uid,
                            name: $"{usersById[uid].FirstName} {usersById[uid].LastName}".Trim(),
                            n: WorkedCount(assignments, uid, cfg.Id)))
                        .ToList();
                    var min = counts.Min(c => c.n);
                    var max = counts.Max(c => c.n);
                    if (max - min <= 1) continue;

                    var rich = counts.First(c => c.n == max);
                    var poor = counts.First(c => c.n == min);
                    warnings.Add(
                        $"Écart « {cfg.Label} » ({levelLabel}) : {rich.name} a {rich.n}, {poor.name} a {poor.n}");
                }
            }
        }

        return warnings;
    }
}
