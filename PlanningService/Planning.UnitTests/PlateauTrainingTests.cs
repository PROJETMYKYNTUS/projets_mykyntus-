using Planning.Domain.Entities;
using Planning.Domain.Enums;
using Planning.Infrastructure.Services;

namespace Planning.UnitTests;

public class PlateauTrainingTests
{
    private static User MakeUser(int id, int level, bool plateau = false) => new()
    {
        Id = id,
        FirstName = $"U{id}",
        LastName = "T",
        Email = $"u{id}@t.ma",
        RoleId = 1,
        IsActive = true,
        SubServiceId = 1,
        Level = level,
        IsPlateauTraining = plateau,
        PasswordHash = "x",
        HireDate = DateTime.UtcNow.AddYears(-1),
    };

    private static SubServiceShiftConfig Shift(int id, ShiftKind kind, string label, int hour) => new()
    {
        Id = id,
        SubServiceId = 1,
        Label = label,
        StartTime = new TimeOnly(hour, 0),
        WorkHours = 8,
        RequiredCount = 2,
        DisplayOrder = id,
        ShiftKind = kind,
        IsTemplate = true,
    };

    [Fact]
    public void Select_plateau_training_prefers_standard_not_opening()
    {
        var opening = Shift(1, ShiftKind.Opening, "8h", 8);
        var mid = Shift(2, ShiftKind.Standard, "9h", 9);
        var closing = Shift(3, ShiftKind.Closing, "11h", 11);
        var users = new Dictionary<int, User> { [10] = MakeUser(10, 2, plateau: true) };
        var history = new Dictionary<int, List<int>>();
        var counts = new Dictionary<int, int>();

        for (var i = 0; i < 6; i++)
        {
            var r = ShiftDispersionSelector.Select(
                [opening, mid, closing], 0, i, 10, history, counts, users);
            Assert.Equal(ShiftKind.Standard, r.Shift.ShiftKind);
            counts[r.Shift.Id] = counts.GetValueOrDefault(r.Shift.Id, 0) + 1;
            if (!history.TryGetValue(10, out var h))
            {
                h = new List<int>();
                history[10] = h;
            }
            h.Add(r.Shift.Id);
        }
    }

    [Fact]
    public void Repair_moves_plateau_training_off_opening_even_with_senior()
    {
        var monday = new DateOnly(2026, 7, 20);
        var cfgOpen = Shift(1, ShiftKind.Opening, "8h", 8);
        var cfgStd = Shift(2, ShiftKind.Standard, "9h", 9);
        var cfgClose = Shift(3, ShiftKind.Closing, "11h", 11);
        var trainee = MakeUser(1, 2, plateau: true);
        var senior = MakeUser(2, 3, plateau: false);
        var other = MakeUser(3, 3, plateau: false);
        var users = new Dictionary<int, User> { [1] = trainee, [2] = senior, [3] = other };
        var planning = new WeeklyPlanning { Id = 1, WeekStartDate = monday, SubServiceId = 1 };
        var assignments = new List<ShiftAssignment>
        {
            new()
            {
                WeeklyPlanningId = 1, UserId = 1, AssignedDate = monday,
                SubServiceShiftConfigId = 1, IsOnLeave = false, IsHoliday = false
            },
            new()
            {
                WeeklyPlanningId = 1, UserId = 2, AssignedDate = monday,
                SubServiceShiftConfigId = 1, IsOnLeave = false, IsHoliday = false
            },
            new()
            {
                WeeklyPlanningId = 1, UserId = 3, AssignedDate = monday,
                SubServiceShiftConfigId = 2, IsOnLeave = false, IsHoliday = false
            },
        };

        LevelBalanceRepairer.Repair(
            assignments, [cfgOpen, cfgStd, cfgClose], users, users.Values.ToList(), planning);

        Assert.Equal(2, assignments.First(a => a.UserId == 1).SubServiceShiftConfigId);
        Assert.Equal(2, assignments.Count(a => a.SubServiceShiftConfigId == 1));
        Assert.Equal(1, assignments.Count(a => a.SubServiceShiftConfigId == 2));
    }

    [Fact]
    public void Repair_beginner_with_senior_on_opening_stays_ok()
    {
        var monday = new DateOnly(2026, 7, 20);
        var cfgOpen = Shift(1, ShiftKind.Opening, "8h", 8);
        var cfgStd = Shift(2, ShiftKind.Standard, "9h", 9);
        var beginner = MakeUser(1, 1, plateau: false);
        var senior = MakeUser(2, 2, plateau: false);
        var users = new Dictionary<int, User> { [1] = beginner, [2] = senior };
        var planning = new WeeklyPlanning { Id = 1, WeekStartDate = monday, SubServiceId = 1 };
        var assignments = new List<ShiftAssignment>
        {
            new()
            {
                WeeklyPlanningId = 1, UserId = 1, AssignedDate = monday,
                SubServiceShiftConfigId = 1, IsOnLeave = false, IsHoliday = false
            },
            new()
            {
                WeeklyPlanningId = 1, UserId = 2, AssignedDate = monday,
                SubServiceShiftConfigId = 1, IsOnLeave = false, IsHoliday = false
            },
        };

        LevelBalanceRepairer.Repair(
            assignments, [cfgOpen, cfgStd], users, users.Values.ToList(), planning);

        Assert.Equal(1, assignments.First(a => a.UserId == 1).SubServiceShiftConfigId);
        Assert.Empty(LevelBalanceEvaluator.Evaluate(assignments, [cfgOpen, cfgStd], users));
    }

    [Fact]
    public void Swap_refuses_placing_plateau_training_on_closing()
    {
        var monday = new DateOnly(2026, 7, 20);
        var opening = Shift(1, ShiftKind.Opening, "8h", 8);
        var closing = Shift(2, ShiftKind.Closing, "11h", 11);
        var users = new Dictionary<int, User>
        {
            [1] = MakeUser(1, 2, plateau: true),
            [2] = MakeUser(2, 2, plateau: false),
        };
        var a = new ShiftAssignment
        {
            UserId = 1, AssignedDate = monday, SubServiceShiftConfigId = 1
        };
        var b = new ShiftAssignment
        {
            UserId = 2, AssignedDate = monday, SubServiceShiftConfigId = 2
        };

        Assert.False(ShiftDispersionSelector.SwapPreservesPlateauTrainingRule(
            a, b, [opening, closing], users));
    }
}
