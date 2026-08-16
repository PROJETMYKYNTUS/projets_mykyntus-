using Planning.Domain.Entities;
using Planning.Domain.Enums;

namespace Planning.Infrastructure.Services;

/// <summary>
/// Redistribue les Confirmés/Experts pour qu'aucun débutant ne reste seul sur un créneau Lun–Ven.
/// Si aucun senior n'est disponible : déplace les débutants seuls d'Opening/Closing vers Standard (milieu).
/// Samedi : l'alternance ON/OFF des Confirmés/Experts n'est jamais cassée.
/// </summary>
public static class LevelBalanceRepairer
{
    private const int MaxPasses = 12;

    public static void Repair(
        List<ShiftAssignment> assignments,
        IReadOnlyList<SubServiceShiftConfig> shiftConfigs,
        IReadOnlyDictionary<int, User> usersById,
        IReadOnlyList<User> roster,
        WeeklyPlanning planning)
    {
        // Semaine uniquement pour débutants. Formation plateau : aussi samedi (créneau).
        RepairWeekdays(assignments, shiftConfigs, usersById);
        RepairPlateauTrainingOffExtremes(assignments, shiftConfigs, usersById);
        _ = roster;
        _ = planning;
    }

    /// <summary>
    /// Formation plateau : jamais Opening/Closing (même avec senior), hors pin manager.
    /// </summary>
    private static void RepairPlateauTrainingOffExtremes(
        List<ShiftAssignment> assignments,
        IReadOnlyList<SubServiceShiftConfig> shiftConfigs,
        IReadOnlyDictionary<int, User> usersById)
    {
        var configsById = shiftConfigs.ToDictionary(c => c.Id);
        var middle = PickMiddleTarget(assignments, shiftConfigs, default, excludeShiftId: -1);
        if (middle == null) return;

        var dates = assignments
            .Where(a => a.SubServiceShiftConfigId != null && !a.IsOnLeave && !a.IsHoliday)
            .Select(a => a.AssignedDate)
            .Distinct()
            .ToList();

        foreach (var date in dates)
        {
            for (var pass = 0; pass < MaxPasses; pass++)
            {
                var moved = false;
                foreach (var a in assignments.Where(x =>
                             x.AssignedDate == date
                             && x.SubServiceShiftConfigId != null
                             && !x.IsOnLeave
                             && !x.IsHoliday
                             && !x.IsManagerOverride
                             && !x.IsExceptionalRequest))
                {
                    if (!usersById.TryGetValue(a.UserId, out var u) || !u.IsPlateauTraining)
                        continue;
                    if (!configsById.TryGetValue(a.SubServiceShiftConfigId!.Value, out var cfg))
                        continue;
                    if (cfg.ShiftKind is not (ShiftKind.Opening or ShiftKind.Closing))
                        continue;

                    var target = PickMiddleTarget(assignments, shiftConfigs, date, cfg.Id) ?? middle;
                    if (target.Id == a.SubServiceShiftConfigId) continue;
                    a.SubServiceShiftConfigId = target.Id;
                    moved = true;
                    break;
                }

                if (!moved) break;
            }
        }
    }

    private static void RepairWeekdays(
        List<ShiftAssignment> assignments,
        IReadOnlyList<SubServiceShiftConfig> shiftConfigs,
        IReadOnlyDictionary<int, User> usersById)
    {
        var configsById = shiftConfigs.ToDictionary(c => c.Id);
        var orderedConfigs = shiftConfigs
            .OrderBy(c => c.ShiftKind == ShiftKind.Opening ? 0
                : c.ShiftKind == ShiftKind.Closing ? 1 : 2)
            .ThenBy(c => c.StartTime)
            .ThenBy(c => c.DisplayOrder)
            .ToList();

        var weekDates = assignments
            .Where(a => !a.IsSaturday && a.AssignedDate.DayOfWeek != DayOfWeek.Saturday)
            .Select(a => a.AssignedDate)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        foreach (var date in weekDates)
        {
            for (var pass = 0; pass < MaxPasses; pass++)
            {
                var deficient = orderedConfigs
                    .Select(cfg => (
                        Cfg: cfg,
                        Group: assignments.Where(a =>
                            a.AssignedDate == date
                            && a.SubServiceShiftConfigId == cfg.Id
                            && !a.IsOnLeave
                            && !a.IsHoliday).ToList()))
                    .Where(x => x.Group.Count > 0
                                && LevelBalanceEvaluator.HasBeginnerAlone(x.Group, usersById))
                    .ToList();

                if (deficient.Count == 0) break;

                var fixedOne = false;
                foreach (var (cfg, group) in deficient)
                {
                    // 1) Swap si le donneur garde ≥ 1 senior (reçoit le débutant)
                    if (TrySwapWithDonor(assignments, usersById, configsById, date, cfg.Id, group))
                    {
                        fixedOne = true;
                        break;
                    }

                    // 2) Sinon déplacer un senior vers le créneau déficitaire (sans échanger)
                    if (TryMoveSeniorToDeficient(assignments, usersById, configsById, date, cfg.Id))
                    {
                        fixedOne = true;
                        break;
                    }
                }

                if (fixedOne) continue;

                // 3) Pas assez de seniors : débutant seul en extrémité → déplacer vers le milieu
                foreach (var (cfg, group) in deficient)
                {
                    if (cfg.ShiftKind is not (ShiftKind.Opening or ShiftKind.Closing))
                        continue;

                    if (TryMoveAloneBeginnerToMiddle(
                            assignments, shiftConfigs, usersById, date, cfg.Id, group))
                    {
                        fixedOne = true;
                        break;
                    }
                }

                if (!fixedOne) break;
            }
        }
    }

    private static bool TrySwapWithDonor(
        List<ShiftAssignment> assignments,
        IReadOnlyDictionary<int, User> usersById,
        IReadOnlyDictionary<int, SubServiceShiftConfig> configsById,
        DateOnly date,
        int deficientShiftId,
        List<ShiftAssignment> deficientGroup)
    {
        var beginner = deficientGroup.FirstOrDefault(a =>
            !a.IsManagerOverride
            && LevelBalanceEvaluator.IsBeginner(usersById, a.UserId));
        if (beginner == null) return false;

        var donor = FindSwapDonor(assignments, usersById, configsById, date, deficientShiftId);
        if (donor == null) return false;

        (beginner.SubServiceShiftConfigId, donor.SubServiceShiftConfigId) =
            (donor.SubServiceShiftConfigId, beginner.SubServiceShiftConfigId);
        return true;
    }

    /// <summary>
    /// Donneur avec ≥ 2 seniors (après swap, ≥ 1 senior reste avec le débutant reçu).
    /// Préfère un donneur Standard pour que le débutant atterrisse au milieu.
    /// </summary>
    private static ShiftAssignment? FindSwapDonor(
        List<ShiftAssignment> assignments,
        IReadOnlyDictionary<int, User> usersById,
        IReadOnlyDictionary<int, SubServiceShiftConfig> configsById,
        DateOnly date,
        int deficientShiftId)
    {
        ShiftAssignment? best = null;
        var bestScore = int.MinValue;

        foreach (var g in WorkingByShift(assignments, date, deficientShiftId))
        {
            var seniors = g.Where(a =>
                    !a.IsManagerOverride
                    && LevelBalanceEvaluator.IsSenior(usersById, a.UserId))
                .ToList();
            if (seniors.Count < 2) continue;

            var isStandard = configsById.TryGetValue(g.Key, out var cfg)
                             && cfg.ShiftKind == ShiftKind.Standard;
            // Score : priorité Standard, puis plus de seniors surplus
            var score = (isStandard ? 1000 : 0) + seniors.Count;

            foreach (var senior in seniors)
            {
                if (score > bestScore)
                {
                    bestScore = score;
                    best = senior;
                }
            }
        }

        return best;
    }

    private static bool TryMoveSeniorToDeficient(
        List<ShiftAssignment> assignments,
        IReadOnlyDictionary<int, User> usersById,
        IReadOnlyDictionary<int, SubServiceShiftConfig> configsById,
        DateOnly date,
        int deficientShiftId)
    {
        ShiftAssignment? best = null;
        var bestScore = int.MinValue;

        foreach (var g in WorkingByShift(assignments, date, deficientShiftId))
        {
            var seniors = g.Where(a =>
                    !a.IsManagerOverride
                    && LevelBalanceEvaluator.IsSenior(usersById, a.UserId))
                .ToList();
            var beginners = g.Count(a => LevelBalanceEvaluator.IsBeginner(usersById, a.UserId));

            foreach (var senior in seniors)
            {
                var seniorsLeft = seniors.Count - 1;
                // Ne pas laisser des débutants seuls sur le créneau donneur
                if (beginners > 0 && seniorsLeft < 1) continue;

                var isStandard = configsById.TryGetValue(g.Key, out var cfg)
                                 && cfg.ShiftKind == ShiftKind.Standard;
                // Préférer prendre un senior d'un créneau non-Standard (ou surplus) pour garder le milieu couvert
                var score = (isStandard ? 0 : 100) + seniors.Count;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = senior;
                }
            }
        }

        if (best == null) return false;
        best.SubServiceShiftConfigId = deficientShiftId;
        return true;
    }

    /// <summary>
    /// Déplace un débutant seul d'Opening/Closing vers un créneau Standard (milieu).
    /// Préférence métier : mieux seul au plateau qu'en ouverture/fermeture.
    /// </summary>
    private static bool TryMoveAloneBeginnerToMiddle(
        List<ShiftAssignment> assignments,
        IReadOnlyList<SubServiceShiftConfig> shiftConfigs,
        IReadOnlyDictionary<int, User> usersById,
        DateOnly date,
        int deficientShiftId,
        List<ShiftAssignment> deficientGroup)
    {
        var beginner = deficientGroup.FirstOrDefault(a =>
            !a.IsManagerOverride
            && LevelBalanceEvaluator.IsBeginner(usersById, a.UserId));
        if (beginner == null) return false;

        var middleTarget = PickMiddleTarget(assignments, shiftConfigs, date, deficientShiftId);
        if (middleTarget == null) return false;

        beginner.SubServiceShiftConfigId = middleTarget.Id;
        return true;
    }

    /// <summary>
    /// Choisit un Standard cible : déjà peuplé si possible, sinon premier par StartTime / DisplayOrder.
    /// </summary>
    private static SubServiceShiftConfig? PickMiddleTarget(
        List<ShiftAssignment> assignments,
        IReadOnlyList<SubServiceShiftConfig> shiftConfigs,
        DateOnly date,
        int excludeShiftId)
    {
        var standards = shiftConfigs
            .Where(c => c.ShiftKind == ShiftKind.Standard && c.Id != excludeShiftId)
            .OrderBy(c => c.StartTime)
            .ThenBy(c => c.DisplayOrder)
            .ToList();

        if (standards.Count == 0) return null;

        var populated = standards
            .Select(c => (
                Cfg: c,
                Count: assignments.Count(a =>
                    a.AssignedDate == date
                    && a.SubServiceShiftConfigId == c.Id
                    && !a.IsOnLeave
                    && !a.IsHoliday)))
            .Where(x => x.Count > 0)
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Cfg.StartTime)
            .ThenBy(x => x.Cfg.DisplayOrder)
            .Select(x => x.Cfg)
            .FirstOrDefault();

        return populated ?? standards[0];
    }

    private static IEnumerable<IGrouping<int, ShiftAssignment>> WorkingByShift(
        List<ShiftAssignment> assignments,
        DateOnly date,
        int excludeShiftId) =>
        assignments
            .Where(a =>
                a.AssignedDate == date
                && a.SubServiceShiftConfigId != null
                && a.SubServiceShiftConfigId != excludeShiftId
                && !a.IsOnLeave
                && !a.IsHoliday
                && !a.IsManagerOverride)
            .GroupBy(a => a.SubServiceShiftConfigId!.Value);
}
