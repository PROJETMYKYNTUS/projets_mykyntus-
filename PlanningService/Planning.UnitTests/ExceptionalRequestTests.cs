using Planning.Domain.Entities;
using Planning.Infrastructure.Services;

namespace Planning.UnitTests;

public class ExceptionalRequestDeadlineTests
{
    private static PlanningAutoGenerateSettings ThursdaySettings() => new()
    {
        DayOfWeek = (int)DayOfWeek.Thursday,
        TimeZone = "Africa/Casablanca",
        Target = "NextWeek"
    };

    private static PlanningAutoGenerateSettings FridaySettings() => new()
    {
        DayOfWeek = (int)DayOfWeek.Friday,
        TimeZone = "Africa/Casablanca",
        Target = "NextWeek"
    };

    [Fact]
    public void EnsureCreationDeadline_thursday_gen_allows_wednesday_morning()
    {
        var settings = ThursdaySettings();
        // Mercredi 2026-07-22 10:00 UTC ≈ encore avant mercredi 23:59 Casablanca
        var utc = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        PlanningExceptionalRequestService.EnsureCreationDeadline(settings, utc);
    }

    [Fact]
    public void EnsureCreationDeadline_thursday_gen_blocks_on_generation_day()
    {
        var settings = ThursdaySettings();
        // Jeudi 23/07/2026 matin UTC → jour de génération, deadline mercredi déjà passée
        var utc = new DateTime(2026, 7, 23, 8, 0, 0, DateTimeKind.Utc);
        var ex = Assert.Throws<InvalidOperationException>(
            () => PlanningExceptionalRequestService.EnsureCreationDeadline(settings, utc));
        Assert.Contains("Délai dépassé", ex.Message);
    }

    [Fact]
    public void EnsureCreationDeadline_blocks_just_after_wednesday_2359_casablanca()
    {
        var settings = ThursdaySettings();
        // 22:59:59.500 n'existe pas en DateTime ticks facilement ;
        // 23:00 UTC mercredi = 00:00 jeudi Casablanca (UTC+1) → jour de gen → bloqué
        var after = new DateTime(2026, 7, 22, 23, 0, 0, DateTimeKind.Utc);
        var ex = Assert.Throws<InvalidOperationException>(
            () => PlanningExceptionalRequestService.EnsureCreationDeadline(settings, after));
        Assert.Contains("Délai dépassé", ex.Message);
    }

    [Fact]
    public void ComputeDeadlineLocal_friday_gen_is_thursday()
    {
        var settings = FridaySettings();
        // Lundi 20/07/2026 → génération vendredi 24 → deadline jeudi 23 23:59
        var deadline = PlanningExceptionalRequestService.ComputeDeadlineLocal(
            settings, new DateOnly(2026, 7, 20));
        Assert.Equal(new DateOnly(2026, 7, 23), DateOnly.FromDateTime(deadline));
        Assert.Equal(23, deadline.Hour);
    }

    [Fact]
    public void ComputeTargetWeek_before_thursday_deadline_targets_next_week_from_gen_day()
    {
        var settings = ThursdaySettings();
        // Mardi 21/07/2026 10:00 UTC — génération jeudi 23 → NextWeek = lundi 27/07
        var utc = new DateTime(2026, 7, 21, 10, 0, 0, DateTimeKind.Utc);
        var target = PlanningExceptionalRequestService.ComputeTargetWeek(settings, utc);
        Assert.Equal(new DateOnly(2026, 7, 27), target.WeekStartDate);
        Assert.False(target.DeadlinePassed);
        Assert.StartsWith("2026-W", target.WeekCode);
        Assert.Equal(PlanningExceptionalRequestService.AvailableWeeksHorizon, target.AvailableWeeks.Count);
        Assert.Contains(target.AvailableWeeks, w => w.Kind == "CurrentWeek");
        Assert.Contains(target.AvailableWeeks, w => w.IsPreferred && w.WeekStartDate == new DateOnly(2026, 7, 27));
        Assert.True(target.AvailableWeeks.Last().WeekStartDate >= new DateOnly(2026, 7, 27).AddDays(7 * 6));
    }

    [Fact]
    public void EnsureCreationDeadline_allows_far_future_week_after_deadline()
    {
        var settings = ThursdaySettings();
        // Jeudi 23/07/2026 = jour de génération, deadline passée pour la semaine imminente
        var utc = new DateTime(2026, 7, 23, 8, 0, 0, DateTimeKind.Utc);
        // Semaine imminente NextWeek = 27/07 ; une semaine plus loin = 03/08 → autorisée
        PlanningExceptionalRequestService.EnsureCreationDeadline(
            settings, utc, new DateOnly(2026, 8, 3));
    }
}

public class ExceptionalRequestPinRepairTests
{
    [Fact]
    public void RepairFairness_skips_manager_override_pins()
    {
        var shiftA = new SubServiceShiftConfig
        {
            Id = 1, Label = "A", StartTime = new TimeOnly(8, 0), DisplayOrder = 0, RequiredCount = 1
        };
        var shiftB = new SubServiceShiftConfig
        {
            Id = 2, Label = "B", StartTime = new TimeOnly(11, 0), DisplayOrder = 1, RequiredCount = 1
        };
        var shifts = new List<SubServiceShiftConfig> { shiftA, shiftB };

        var u1 = new User { Id = 10, Level = 2, FirstName = "A", LastName = "1" };
        var u2 = new User { Id = 11, Level = 2, FirstName = "B", LastName = "2" };
        var users = new Dictionary<int, User> { [10] = u1, [11] = u2 };

        var monday = new DateOnly(2026, 7, 27);
        var assignments = new List<ShiftAssignment>
        {
            new()
            {
                UserId = 10,
                AssignedDate = monday,
                SubServiceShiftConfigId = 2, // pinned 11h
                IsManagerOverride = true
            },
            new()
            {
                UserId = 11,
                AssignedDate = monday,
                SubServiceShiftConfigId = 1,
                IsManagerOverride = false
            },
            // autre jour pour créer déséquilibre fairness potentiel
            new()
            {
                UserId = 10,
                AssignedDate = monday.AddDays(1),
                SubServiceShiftConfigId = 2,
                IsManagerOverride = false
            },
            new()
            {
                UserId = 11,
                AssignedDate = monday.AddDays(1),
                SubServiceShiftConfigId = 1,
                IsManagerOverride = false
            },
        };

        ShiftDispersionSelector.RepairFairness(assignments, shifts, users);
        ShiftDispersionSelector.RepairWeekdayDispersion(assignments, shifts, users);

        var pinned = assignments.First(a => a.UserId == 10 && a.AssignedDate == monday);
        Assert.True(pinned.IsManagerOverride);
        Assert.Equal(2, pinned.SubServiceShiftConfigId);
    }

    [Fact]
    public void LevelBalanceRepair_does_not_move_pinned_beginner()
    {
        var opening = new SubServiceShiftConfig
        {
            Id = 1, Label = "Open", StartTime = new TimeOnly(7, 0),
            DisplayOrder = 0, ShiftKind = Planning.Domain.Enums.ShiftKind.Opening, RequiredCount = 1
        };
        var standard = new SubServiceShiftConfig
        {
            Id = 2, Label = "Mid", StartTime = new TimeOnly(11, 0),
            DisplayOrder = 1, ShiftKind = Planning.Domain.Enums.ShiftKind.Standard, RequiredCount = 2
        };
        var shifts = new List<SubServiceShiftConfig> { opening, standard };

        var beginner = new User { Id = 1, Level = 1, FirstName = "Beg", LastName = "1" };
        var senior1 = new User { Id = 2, Level = 3, FirstName = "Sen", LastName = "1" };
        var senior2 = new User { Id = 3, Level = 3, FirstName = "Sen", LastName = "2" };
        var users = new Dictionary<int, User>
        {
            [1] = beginner, [2] = senior1, [3] = senior2
        };

        var date = new DateOnly(2026, 7, 28);
        var assignments = new List<ShiftAssignment>
        {
            new()
            {
                UserId = 1, AssignedDate = date, SubServiceShiftConfigId = 1,
                IsManagerOverride = true // pin opening
            },
            new()
            {
                UserId = 2, AssignedDate = date, SubServiceShiftConfigId = 2
            },
            new()
            {
                UserId = 3, AssignedDate = date, SubServiceShiftConfigId = 2
            },
        };

        var planning = new WeeklyPlanning
        {
            Id = 1,
            WeekCode = "2026-W31",
            WeekStartDate = new DateOnly(2026, 7, 27),
            SubServiceId = 1
        };

        LevelBalanceRepairer.Repair(assignments, shifts, users, users.Values.ToList(), planning);

        var pinned = assignments.First(a => a.UserId == 1);
        Assert.Equal(1, pinned.SubServiceShiftConfigId);
        Assert.True(pinned.IsManagerOverride);
    }
}
