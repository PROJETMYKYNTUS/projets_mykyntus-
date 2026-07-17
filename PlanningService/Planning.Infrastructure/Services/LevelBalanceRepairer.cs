using Planning.Domain.Entities;
using Planning.Domain.Enums;

namespace Planning.Infrastructure.Services;

/// <summary>
/// Redistribue les Confirmés/Experts pour qu'aucun débutant ne reste seul sur un créneau Lun–Ven.
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
        // Semaine uniquement. Samedi : ne jamais forcer un Confirmé/Expert « Off »
        // (alternance ON/OFF préservée). Si débutants seuls → anomalie Warning non bloquante.
        RepairWeekdays(assignments, shiftConfigs, usersById);
        _ = roster;
        _ = planning;
    }

    private static void RepairWeekdays(
        List<ShiftAssignment> assignments,
        IReadOnlyList<SubServiceShiftConfig> shiftConfigs,
        IReadOnlyDictionary<int, User> usersById)
    {
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
                    if (TrySwapWithDonor(assignments, usersById, date, cfg.Id, group))
                    {
                        fixedOne = true;
                        break;
                    }

                    // 2) Sinon déplacer un senior vers le créneau déficitaire (sans échanger)
                    if (TryMoveSeniorToDeficient(assignments, usersById, date, cfg.Id))
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
        DateOnly date,
        int deficientShiftId,
        List<ShiftAssignment> deficientGroup)
    {
        var beginner = deficientGroup.FirstOrDefault(a =>
            LevelBalanceEvaluator.IsBeginner(usersById, a.UserId));
        if (beginner == null) return false;

        var donor = FindSwapDonor(assignments, usersById, date, deficientShiftId);
        if (donor == null) return false;

        (beginner.SubServiceShiftConfigId, donor.SubServiceShiftConfigId) =
            (donor.SubServiceShiftConfigId, beginner.SubServiceShiftConfigId);
        return true;
    }

    /// <summary>Donneur avec ≥ 2 seniors (après swap, ≥ 1 senior reste avec le débutant reçu).</summary>
    private static ShiftAssignment? FindSwapDonor(
        List<ShiftAssignment> assignments,
        IReadOnlyDictionary<int, User> usersById,
        DateOnly date,
        int deficientShiftId)
    {
        ShiftAssignment? best = null;
        var bestScore = -1;

        foreach (var g in WorkingByShift(assignments, date, deficientShiftId))
        {
            var seniors = g.Where(a => LevelBalanceEvaluator.IsSenior(usersById, a.UserId)).ToList();
            if (seniors.Count < 2) continue;

            foreach (var senior in seniors)
            {
                if (seniors.Count > bestScore)
                {
                    bestScore = seniors.Count;
                    best = senior;
                }
            }
        }

        return best;
    }

    private static bool TryMoveSeniorToDeficient(
        List<ShiftAssignment> assignments,
        IReadOnlyDictionary<int, User> usersById,
        DateOnly date,
        int deficientShiftId)
    {
        foreach (var g in WorkingByShift(assignments, date, deficientShiftId))
        {
            var seniors = g.Where(a => LevelBalanceEvaluator.IsSenior(usersById, a.UserId)).ToList();
            var beginners = g.Count(a => LevelBalanceEvaluator.IsBeginner(usersById, a.UserId));

            foreach (var senior in seniors)
            {
                var seniorsLeft = seniors.Count - 1;
                // Ne pas laisser des débutants seuls sur le créneau donneur
                if (beginners > 0 && seniorsLeft < 1) continue;

                senior.SubServiceShiftConfigId = deficientShiftId;
                return true;
            }
        }

        return false;
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
                && !a.IsHoliday)
            .GroupBy(a => a.SubServiceShiftConfigId!.Value);
}
