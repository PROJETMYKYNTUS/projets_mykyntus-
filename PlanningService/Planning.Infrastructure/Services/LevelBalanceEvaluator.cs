using Planning.Application.DTOs.Planning;
using Planning.Domain.Entities;
using Planning.Domain.Enums;

namespace Planning.Infrastructure.Services;

/// <summary>
/// Règle plateau : un débutant (Level 1) ne doit pas être seul sur un créneau —
/// au moins un Confirmé/Expert (Level &gt;= 2) sur le même shift (Lun–Ven, tous ShiftKind).
/// Samedi : sur l'ensemble des présents du jour (demi-journées incluses).
/// </summary>
public static class LevelBalanceEvaluator
{
    public const string AnomalyCode = "LEVEL_BALANCE";

    public static void ApplyShiftKindsFromStartTimes(IList<SubServiceShiftConfig> configs)
    {
        if (configs.Count == 0) return;

        foreach (var c in configs)
            c.ShiftKind = ShiftKind.Standard;

        if (configs.Count == 1)
        {
            configs[0].ShiftKind = ShiftKind.Opening;
            return;
        }

        var ordered = configs.OrderBy(c => c.StartTime).ThenBy(c => c.DisplayOrder).ToList();
        var minStart = ordered.First().StartTime;
        var maxStart = ordered.Last().StartTime;

        foreach (var c in ordered.Where(c => c.StartTime == minStart))
            c.ShiftKind = ShiftKind.Opening;

        foreach (var c in ordered.Where(c => c.StartTime == maxStart && c.ShiftKind != ShiftKind.Opening))
            c.ShiftKind = ShiftKind.Closing;
    }

    public static List<PlanningAnomalyDto> Evaluate(
        IEnumerable<ShiftAssignment> assignments,
        IEnumerable<SubServiceShiftConfig> shiftConfigs,
        IReadOnlyDictionary<int, User> usersById,
        IReadOnlyList<User>? roster = null)
    {
        var configsById = shiftConfigs.ToDictionary(c => c.Id);
        var anomalies = new List<PlanningAnomalyDto>();
        var list = assignments.ToList();
        roster ??= usersById.Values.ToList();

        var weekdayGroups = list
            .Where(a =>
                a.SubServiceShiftConfigId != null
                && !a.IsOnLeave
                && !a.IsHoliday
                && !a.IsSaturday
                && a.AssignedDate.DayOfWeek != DayOfWeek.Saturday)
            .GroupBy(a => (a.AssignedDate, a.SubServiceShiftConfigId!.Value));

        foreach (var group in weekdayGroups)
        {
            if (!configsById.TryGetValue(group.Key.Item2, out var cfg))
                continue;

            if (!HasBeginnerAlone(group, usersById))
                continue;

            var date = group.Key.Item1;
            var dayName = date.DayOfWeek.ToString();
            var context = cfg.ShiftKind switch
            {
                ShiftKind.Opening => "ouverture",
                ShiftKind.Closing => "fermeture",
                _ => "plateau"
            };
            var forced = !HasPresentableSeniorOnDate(list, usersById, date, roster);
            anomalies.Add(MakeAnomaly(
                date, dayName, cfg.Id, cfg.Label, forced,
                $"{dayName} {date:dd/MM} — {cfg.Label} ({context}) : " +
                "débutant(s) sans Confirmé/Expert sur le même créneau (plateau)."));
        }

        var saturdayByDate = list
            .Where(a =>
                a.SubServiceShiftConfigId != null
                && !a.IsOnLeave
                && !a.IsHoliday
                && (a.IsSaturday || a.AssignedDate.DayOfWeek == DayOfWeek.Saturday))
            .GroupBy(a => a.AssignedDate);

        foreach (var dayGroup in saturdayByDate)
        {
            if (!HasBeginnerAlone(dayGroup, usersById))
                continue;

            var first = dayGroup.First();
            configsById.TryGetValue(first.SubServiceShiftConfigId ?? 0, out var cfg);
            var forced = !HasPresentableSeniorOnDate(list, usersById, dayGroup.Key, roster);
            anomalies.Add(MakeAnomaly(
                dayGroup.Key, "Saturday", cfg?.Id ?? 0, cfg?.Label ?? "Samedi", forced,
                $"Saturday {dayGroup.Key:dd/MM} — samedi : débutant(s) sans Confirmé/Expert présent."));
        }

        return anomalies;
    }

    public static bool HasBeginnerAlone(
        IEnumerable<ShiftAssignment> group,
        IReadOnlyDictionary<int, User> usersById)
    {
        var levels = group
            .Select(a => usersById.TryGetValue(a.UserId, out var u) ? u.Level : 1)
            .ToList();
        return levels.Any(l => l == 1) && !levels.Any(l => l >= 2);
    }

    public static int GetLevel(IReadOnlyDictionary<int, User> usersById, int userId) =>
        usersById.TryGetValue(userId, out var u) ? u.Level : 1;

    public static bool IsSenior(IReadOnlyDictionary<int, User> usersById, int userId) =>
        GetLevel(usersById, userId) >= 2;

    public static bool IsBeginner(IReadOnlyDictionary<int, User> usersById, int userId) =>
        GetLevel(usersById, userId) == 1;

    /// <summary>
    /// Confirmé/Expert réellement présent au travail ce jour (créneau assigné, non congé / non férié).
    /// Un Confirmé « Off » samedi (alternance) n’est pas présentable — on ne le force pas à travailler.
    /// </summary>
    public static bool HasPresentableSeniorOnDate(
        IEnumerable<ShiftAssignment> assignments,
        IReadOnlyDictionary<int, User> usersById,
        DateOnly date,
        IReadOnlyList<User>? roster = null)
    {
        _ = roster;
        return assignments.Any(a =>
            a.AssignedDate == date
            && a.SubServiceShiftConfigId != null
            && !a.IsOnLeave
            && !a.IsHoliday
            && IsSenior(usersById, a.UserId));
    }

    private static PlanningAnomalyDto MakeAnomaly(
        DateOnly date, string day, int shiftConfigId, string shiftLabel, bool isForced, string message) => new()
    {
        Code = AnomalyCode,
        Severity = "Warning",
        Date = date,
        Day = day,
        ShiftConfigId = shiftConfigId,
        ShiftLabel = shiftLabel,
        Message = message,
        IsForced = isForced
    };
}
