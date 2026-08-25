using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Planning.Application.DTOs.Planning;
using Planning.Domain.Entities;
using Planning.Domain.Enums;
using Planning.Infrastructure.Hubs;
using Planning.Infrastructure.Persistence;
using Planning.Infrastructure.Services;

namespace Planning.UnitTests;

public class WeekdayDispersionFairnessTests
{
    private static AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    private static PlanningService CreateService(AppDbContext db) =>
        new(db, new FakePlanningHubContext(), new PlanningPerimeterResolver(db));

    private static async Task SeedBaseAsync(AppDbContext db)
    {
        db.Roles.Add(new Role { Id = 1, Name = "Pilote" });
        db.Floors.Add(new Floor { Id = 1, Name = "Floor 1", FloorNumber = 1 });
        db.Services.Add(new Service { Id = 1, FloorId = 1, Name = "Svc", Code = "S1" });
        db.SubServices.Add(new SubService { Id = 1, ServiceId = 1, Name = "Cellule A", Code = "CA" });
        await db.SaveChangesAsync();
    }

    private static User MakeUser(int id, string email, int level) => new()
    {
        Id = id,
        FirstName = $"U{id}",
        LastName = "Test",
        Email = email,
        RoleId = 1,
        IsActive = true,
        SubServiceId = 1,
        Level = level,
        PasswordHash = "x",
        HireDate = DateTime.UtcNow.AddYears(-1),
    };

    private static SubServiceShiftConfig Shift(int id, string label, int hour, int required, int? modeId = null) => new()
    {
        Id = id,
        SubServiceId = 1,
        Label = label,
        StartTime = new TimeOnly(hour, 0),
        WorkHours = 8,
        RequiredCount = required,
        DisplayOrder = id,
        ShiftKind = ShiftKind.Standard,
        IsTemplate = true,
        ShiftModeProfileId = modeId,
    };

    [Fact]
    public void BuildSeeds_interleaves_same_level()
    {
        var employees = new List<User>
        {
            MakeUser(1, "a@t.ma", 2),
            MakeUser(2, "b@t.ma", 2),
            MakeUser(3, "c@t.ma", 2),
        };
        var shifts = new List<SubServiceShiftConfig>
        {
            Shift(10, "8h", 8, 0),
            Shift(11, "9h", 9, 0),
            Shift(12, "10h", 10, 0),
        };

        var seeds = WeekShiftPatternAssigner.BuildSeeds(employees, _ => shifts, weekNumber: 0);

        Assert.Equal(0, seeds[1]);
        Assert.Equal(1, seeds[2]);
        Assert.Equal(2, seeds[3]);
    }

    [Fact]
    public void BuildSeeds_does_not_collide_first_of_each_level()
    {
        var employees = new List<User>
        {
            MakeUser(1, "d@t.ma", 1),
            MakeUser(2, "c1@t.ma", 2),
            MakeUser(3, "c2@t.ma", 2),
            MakeUser(4, "e1@t.ma", 3),
            MakeUser(5, "e2@t.ma", 3),
        };
        var shifts = new List<SubServiceShiftConfig>
        {
            Shift(10, "8h", 8, 0),
            Shift(11, "9h", 9, 0),
            Shift(12, "10h", 10, 0),
            Shift(13, "11h", 11, 0),
        };

        var seeds = WeekShiftPatternAssigner.BuildSeeds(employees, _ => shifts, weekNumber: 0);

        Assert.NotEqual(seeds[1], seeds[2]);
        Assert.NotEqual(seeds[1], seeds[4]);
        Assert.Equal(4, new[] { seeds[1], seeds[2], seeds[3], seeds[4] }.Distinct().Count());
    }

    [Fact]
    public void RepairFairness_fixes_three_vs_one_same_level()
    {
        var monday = new DateOnly(2026, 7, 20);
        var shifts = new List<SubServiceShiftConfig>
        {
            Shift(1, "8h", 8, 1),
            Shift(2, "9h", 9, 1),
            Shift(3, "10h", 10, 1),
            Shift(4, "11h", 11, 1),
        };
        var users = new Dictionary<int, User>
        {
            [1] = MakeUser(1, "a@t.ma", 3),
            [2] = MakeUser(2, "b@t.ma", 3),
        };
        var assignments = new List<ShiftAssignment>();
        int[] expert1 = [2, 3, 2, 1, 2];
        int[] expert2 = [3, 4, 1, 2, 4];
        for (var d = 0; d < 5; d++)
        {
            assignments.Add(new ShiftAssignment
            {
                UserId = 1,
                AssignedDate = monday.AddDays(d),
                SubServiceShiftConfigId = expert1[d],
                IsOnLeave = false,
                IsHoliday = false
            });
            assignments.Add(new ShiftAssignment
            {
                UserId = 2,
                AssignedDate = monday.AddDays(d),
                SubServiceShiftConfigId = expert2[d],
                IsOnLeave = false,
                IsHoliday = false
            });
        }

        ShiftDispersionSelector.RepairWeekQuality(assignments, shifts, users);

        var nineCounts = new[]
        {
            assignments.Count(a => a.UserId == 1 && a.SubServiceShiftConfigId == 2),
            assignments.Count(a => a.UserId == 2 && a.SubServiceShiftConfigId == 2)
        };
        Assert.True(nineCounts.Max() - nineCounts.Min() <= 1, string.Join(",", nineCounts));
        Assert.True(nineCounts.Max() <= 2, string.Join(",", nineCounts));
    }

    [Fact]
    public void AssignDay_moves_surplus_to_fill_quota_without_breaking_max2()
    {
        var employees = new List<User>
        {
            MakeUser(1, "a@t.ma", 2),
            MakeUser(2, "b@t.ma", 2),
            MakeUser(3, "c@t.ma", 2),
        };
        var a = Shift(1, "8h", 8, 2);
        var b = Shift(2, "11h", 11, 1);
        var shifts = new List<SubServiceShiftConfig> { a, b };
        var users = employees.ToDictionary(e => e.Id);
        var seeds = employees.ToDictionary(e => e.Id, _ => 0);
        var history = new Dictionary<int, List<int>>();

        var assigned = WeekShiftPatternAssigner.AssignDay(
            employees, _ => shifts, seeds, history, users,
            new Dictionary<int, SubServiceShiftConfig>(), shifts);

        Assert.Equal(2, assigned.Values.Count(s => s.Id == 1));
        Assert.Equal(1, assigned.Values.Count(s => s.Id == 2));
    }

    [Fact]
    public void AssignDay_does_not_stack_on_first_shift_when_another_quota_is_empty()
    {
        var employees = new List<User>
        {
            MakeUser(1, "a@t.ma", 2),
            MakeUser(2, "b@t.ma", 2),
        };
        var shifts = new List<SubServiceShiftConfig>
        {
            Shift(1, "8h", 8, 1),
            Shift(2, "9h", 9, 1),
            Shift(3, "10h", 10, 0),
        };
        var users = employees.ToDictionary(e => e.Id);
        var seeds = employees.ToDictionary(e => e.Id, _ => 0);
        var history = new Dictionary<int, List<int>>();

        var assigned = WeekShiftPatternAssigner.AssignDay(
            employees, _ => shifts, seeds, history, users,
            new Dictionary<int, SubServiceShiftConfig>(), shifts);

        Assert.Equal(1, assigned.Values.Count(s => s.Id == 1));
        Assert.Equal(1, assigned.Values.Count(s => s.Id == 2));
        Assert.Equal(0, assigned.Values.Count(s => s.Id == 3));
    }

    [Fact]
    public void AssignDay_breaks_successive_same_shift_when_a_swap_exists()
    {
        var employees = new List<User>
        {
            MakeUser(1, "a@t.ma", 2),
            MakeUser(2, "b@t.ma", 2),
        };
        var s8 = Shift(1, "8h", 8, 1);
        var s9 = Shift(2, "9h", 9, 1);
        var shifts = new List<SubServiceShiftConfig> { s8, s9 };
        var users = employees.ToDictionary(e => e.Id);
        var seeds = new Dictionary<int, int> { [1] = 0, [2] = 1 };
        var history = new Dictionary<int, List<int>>
        {
            [1] = [1],
            [2] = [2],
        };

        var assigned = WeekShiftPatternAssigner.AssignDay(
            employees, _ => shifts, seeds, history, users,
            new Dictionary<int, SubServiceShiftConfig>(), shifts);

        Assert.NotEqual(1, assigned[1].Id);
        Assert.NotEqual(2, assigned[2].Id);
        Assert.Equal(1, assigned.Values.Count(s => s.Id == 1));
        Assert.Equal(1, assigned.Values.Count(s => s.Id == 2));
    }

    [Fact]
    public void AssignDay_never_makes_a_third_successive_same_shift()
    {
        var employees = new List<User>
        {
            MakeUser(1, "a@t.ma", 2),
            MakeUser(2, "b@t.ma", 2),
        };
        var shifts = new List<SubServiceShiftConfig>
        {
            Shift(1, "8h", 8, 1),
            Shift(2, "9h", 9, 1),
        };
        var users = employees.ToDictionary(e => e.Id);
        var seeds = new Dictionary<int, int> { [1] = 0, [2] = 1 };
        var history = new Dictionary<int, List<int>>
        {
            [1] = [1, 1],
            [2] = [2, 2],
        };

        var assigned = WeekShiftPatternAssigner.AssignDay(
            employees, _ => shifts, seeds, history, users,
            new Dictionary<int, SubServiceShiftConfig>(), shifts);

        Assert.NotEqual(1, assigned[1].Id);
        Assert.NotEqual(2, assigned[2].Id);
    }

    [Fact]
    public void FillUnderQuotas_skips_unfillable_shift_and_still_fills_others()
    {
        var employees = new List<User>
        {
            MakeUser(1, "a@t.ma", 2),
            MakeUser(2, "b@t.ma", 2),
        };
        var ghost = Shift(1, "Ghost", 7, 1);
        ghost.DisplayOrder = 0;
        var s8 = Shift(10, "8h", 8, 1);
        var s9 = Shift(11, "9h", 9, 1);
        var all = new List<SubServiceShiftConfig> { ghost, s8, s9 };
        var allowed = new List<SubServiceShiftConfig> { s8, s9 };
        var users = employees.ToDictionary(e => e.Id);
        var seeds = new Dictionary<int, int> { [1] = 0, [2] = 1 };

        var assigned = WeekShiftPatternAssigner.AssignDay(
            employees, _ => allowed, seeds, new Dictionary<int, List<int>>(), users,
            new Dictionary<int, SubServiceShiftConfig>(), all);

        Assert.Equal(1, assigned.Values.Count(s => s.Id == 10));
        Assert.Equal(1, assigned.Values.Count(s => s.Id == 11));
        Assert.Equal(0, assigned.Values.Count(s => s.Id == 1));
    }

    [Fact]
    public void AssignDay_never_parks_on_zero_quota_while_another_seat_is_open()
    {
        var employees = new List<User>
        {
            MakeUser(1, "aya@t.ma", 2),
            MakeUser(2, "chay@t.ma", 2),
        };
        var shifts = new List<SubServiceShiftConfig>
        {
            Shift(10, "S1", 8, 1),
            Shift(11, "S2", 9, 1),
            Shift(12, "S3", 10, 0),
            Shift(13, "S4", 11, 0),
        };
        var users = employees.ToDictionary(e => e.Id);
        var seeds = new Dictionary<int, int> { [1] = 0, [2] = 0 };
        var history = new Dictionary<int, List<int>>
        {
            [1] = [10, 10],
            [2] = [11, 11],
        };

        var assigned = WeekShiftPatternAssigner.AssignDay(
            employees, _ => shifts, seeds, history, users,
            new Dictionary<int, SubServiceShiftConfig>(), shifts);

        Assert.Equal(1, assigned.Values.Count(s => s.Id == 10));
        Assert.Equal(1, assigned.Values.Count(s => s.Id == 11));
        Assert.Equal(0, assigned.Values.Count(s => s.Id == 12));
        Assert.Equal(0, assigned.Values.Count(s => s.Id == 13));
    }

    [Fact]
    public void Solo_mode_two_shifts_week_even_is_three_plus_two()
    {
        var counts = RunSoloTwoShiftWeek(weekNumber: 0);
        Assert.Equal(3, counts[10]);
        Assert.Equal(2, counts[11]);
    }

    [Fact]
    public void Solo_mode_two_shifts_week_odd_inverts_majority()
    {
        var counts = RunSoloTwoShiftWeek(weekNumber: 1);
        Assert.Equal(2, counts[10]);
        Assert.Equal(3, counts[11]);
    }

    [Fact]
    public void Solo_mode_never_five_days_same_shift()
    {
        foreach (var week in new[] { 0, 1, 35, 36 })
        {
            var counts = RunSoloTwoShiftWeek(week);
            Assert.True(counts[10] <= 3, $"week {week} shift8={counts[10]}");
            Assert.True(counts[11] <= 3, $"week {week} shift9={counts[11]}");
            Assert.Equal(5, counts[10] + counts[11]);
        }
    }

    private static Dictionary<int, int> RunSoloTwoShiftWeek(int weekNumber)
    {
        var emp = MakeUser(1, "solo@t.ma", 2);
        var employees = new List<User> { emp };
        const int modeId = 42;
        var shifts = new List<SubServiceShiftConfig>
        {
            Shift(10, "8h", 8, 0, modeId),
            Shift(11, "9h", 9, 0, modeId),
        };
        var users = employees.ToDictionary(e => e.Id);
        var seeds = WeekShiftPatternAssigner.BuildSeeds(employees, _ => shifts, weekNumber);
        var history = new Dictionary<int, List<int>>();
        var pins = new Dictionary<int, SubServiceShiftConfig>();
        var counts = new Dictionary<int, int> { [10] = 0, [11] = 0 };

        for (var d = 0; d < 5; d++)
        {
            var day = WeekShiftPatternAssigner.AssignDay(
                employees, _ => shifts, seeds, history, users, pins, shifts);
            var sid = day[1].Id;
            counts[sid] = counts.GetValueOrDefault(sid, 0) + 1;
            if (!history.TryGetValue(1, out var hist))
            {
                hist = new List<int>();
                history[1] = hist;
            }
            hist.Add(sid);
        }

        return counts;
    }

    [Fact]
    public void AssignDay_mixed_bte_emails_fills_each_mode_quotas()
    {
        var bte = Enumerable.Range(1, 6).Select(i => MakeUser(i, $"b{i}@t.ma", i == 4 ? 1 : 2)).ToList();
        var emails = new List<User> { MakeUser(7, "aya@t.ma", 2), MakeUser(8, "chay@t.ma", 2) };
        var employees = bte.Concat(emails).ToList();
        const int bteMode = 100;
        const int mailMode = 200;
        var bteShifts = new List<SubServiceShiftConfig>
        {
            Shift(1, "BTE 8h", 8, 2, bteMode),
            Shift(2, "BTE 9h", 9, 1, bteMode),
            Shift(3, "BTE 10h", 10, 1, bteMode),
            Shift(4, "BTE 11h", 11, 1, bteMode),
        };
        var mailShifts = new List<SubServiceShiftConfig>
        {
            Shift(10, "Mail 8h", 8, 1, mailMode),
            Shift(11, "Mail 9h", 9, 1, mailMode),
            Shift(12, "Mail 10h", 10, 0, mailMode),
            Shift(13, "Mail 11h", 11, 0, mailMode),
        };
        var all = bteShifts.Concat(mailShifts).ToList();
        IReadOnlyList<SubServiceShiftConfig> For(int id) =>
            id >= 7 ? mailShifts : bteShifts;

        var users = employees.ToDictionary(e => e.Id);
        var bteSeeds = WeekShiftPatternAssigner.BuildSeeds(bte, For, 35);
        var mailSeeds = WeekShiftPatternAssigner.BuildSeeds(emails, For, 35);
        var seeds = bteSeeds.Concat(mailSeeds).ToDictionary(kv => kv.Key, kv => kv.Value);
        var history = new Dictionary<int, List<int>>();
        var pins = new Dictionary<int, SubServiceShiftConfig>();

        for (var d = 0; d < 5; d++)
        {
            var bteDay = WeekShiftPatternAssigner.AssignDay(
                bte, For, seeds, history, users, pins, bteShifts);
            var mailDay = WeekShiftPatternAssigner.AssignDay(
                emails, For, seeds, history, users, pins, mailShifts);

            Assert.True(bteDay.Values.Count(s => s.Id == 1) >= 2, $"BTE 8h day {d}");
            Assert.True(bteDay.Values.Count(s => s.Id == 2) >= 1, $"BTE 9h day {d}");
            Assert.True(bteDay.Values.Count(s => s.Id == 3) >= 1, $"BTE 10h day {d}");
            Assert.True(bteDay.Values.Count(s => s.Id == 4) >= 1, $"BTE 11h day {d}");
            Assert.Equal(1, mailDay.Values.Count(s => s.Id == 10));
            Assert.Equal(1, mailDay.Values.Count(s => s.Id == 11));
            Assert.Equal(0, mailDay.Values.Count(s => s.Id == 12));
            Assert.Equal(0, mailDay.Values.Count(s => s.Id == 13));

            foreach (var (uid, shift) in bteDay.Concat(mailDay))
            {
                if (!history.TryGetValue(uid, out var hist))
                {
                    hist = new List<int>();
                    history[uid] = hist;
                }
                hist.Add(shift.Id);
            }
        }

        foreach (var uid in new[] { 7, 8 })
        {
            var hist = history[uid];
            var streak = 1;
            for (var i = 1; i < hist.Count; i++)
            {
                if (hist[i] == hist[i - 1]) streak++;
                else streak = 1;
                Assert.True(streak <= 2, $"email user {uid} successive x{streak}");
            }
        }
    }

    [Fact]
    public async Task Generate_four_shifts_same_level_no_triple_and_fair()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Users.AddRange(
            MakeUser(1, "a@test.ma", 2),
            MakeUser(2, "b@test.ma", 2));
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await svc.SaveShiftTemplateAsync(new SaveShiftConfigDto
        {
            SubServiceId = 1,
            Shifts =
            [
                new ShiftConfigItemDto { Label = "8h", StartTime = "08:00", WorkHours = 8, RequiredCount = 0, DisplayOrder = 1 },
                new ShiftConfigItemDto { Label = "9h", StartTime = "09:00", WorkHours = 8, RequiredCount = 0, DisplayOrder = 2 },
                new ShiftConfigItemDto { Label = "10h", StartTime = "10:00", WorkHours = 8, RequiredCount = 0, DisplayOrder = 3 },
                new ShiftConfigItemDto { Label = "11h", StartTime = "11:00", WorkHours = 8, RequiredCount = 0, DisplayOrder = 4 },
            ]
        });

        var monday = new DateOnly(2026, 7, 20);
        var planning = await svc.CreatePlanningAsync(new CreateWeeklyPlanningDto
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeekStartDate = monday,
            TotalEffectif = 2
        });

        await svc.GeneratePlanningFromConfigAsync(new GeneratePlanningFromConfigDto
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeeklyPlanningId = planning.Id
        });

        var rows = await db.ShiftAssignments
            .Where(a => a.WeeklyPlanningId == planning.Id && !a.IsSaturday && !a.IsOnLeave && !a.IsHoliday)
            .ToListAsync();

        AssertNoTripleSameShift(rows);
        AssertSameLevelGapAtMostOne(rows, new[] { 1, 2 });
        AssertNoConsecutiveWorked(rows);
    }

    [Fact]
    public async Task Generate_two_shifts_complementary_seeds_not_both_triple_opening()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Users.AddRange(
            MakeUser(1, "a@test.ma", 2),
            MakeUser(2, "b@test.ma", 2));
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await svc.SaveShiftTemplateAsync(new SaveShiftConfigDto
        {
            SubServiceId = 1,
            Shifts =
            [
                new ShiftConfigItemDto { Label = "8h", StartTime = "08:00", WorkHours = 8, RequiredCount = 1, DisplayOrder = 1 },
                new ShiftConfigItemDto { Label = "11h", StartTime = "11:00", WorkHours = 8, RequiredCount = 1, DisplayOrder = 2 },
            ]
        });

        var monday = new DateOnly(2026, 7, 20);
        var planning = await svc.CreatePlanningAsync(new CreateWeeklyPlanningDto
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeekStartDate = monday,
            TotalEffectif = 2
        });

        await svc.GeneratePlanningFromConfigAsync(new GeneratePlanningFromConfigDto
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeeklyPlanningId = planning.Id
        });

        var configs = await db.SubServiceShiftConfigs
            .Where(c => c.SubServiceId == 1 && !c.IsTemplate)
            .ToListAsync();
        var openId = configs.OrderBy(c => c.StartTime).First().Id;

        var rows = await db.ShiftAssignments
            .Where(a => a.WeeklyPlanningId == planning.Id && !a.IsSaturday && !a.IsOnLeave && !a.IsHoliday)
            .ToListAsync();

        AssertSameLevelGapAtMostOne(rows, new[] { 1, 2 });
        AssertNoConsecutiveWorked(rows);

        var openCounts = rows
            .Where(a => a.SubServiceShiftConfigId == openId)
            .GroupBy(a => a.UserId)
            .Select(g => g.Count())
            .OrderBy(n => n)
            .ToList();
        Assert.Equal(2, openCounts.Count);
        Assert.Equal(2, openCounts[0]);
        Assert.Equal(3, openCounts[1]);
    }

    [Fact]
    public async Task Generate_fills_weekday_quotas_when_headcount_enough()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Users.AddRange(
            MakeUser(1, "a@test.ma", 2),
            MakeUser(2, "b@test.ma", 2),
            MakeUser(3, "c@test.ma", 2),
            MakeUser(4, "d@test.ma", 2));
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await svc.SaveShiftTemplateAsync(new SaveShiftConfigDto
        {
            SubServiceId = 1,
            Shifts =
            [
                new ShiftConfigItemDto { Label = "8h", StartTime = "08:00", WorkHours = 8, RequiredCount = 1, DisplayOrder = 1 },
                new ShiftConfigItemDto { Label = "9h", StartTime = "09:00", WorkHours = 8, RequiredCount = 1, DisplayOrder = 2 },
                new ShiftConfigItemDto { Label = "10h", StartTime = "10:00", WorkHours = 8, RequiredCount = 1, DisplayOrder = 3 },
                new ShiftConfigItemDto { Label = "11h", StartTime = "11:00", WorkHours = 8, RequiredCount = 1, DisplayOrder = 4 },
            ]
        });

        var monday = new DateOnly(2026, 7, 20);
        var planning = await svc.CreatePlanningAsync(new CreateWeeklyPlanningDto
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeekStartDate = monday,
            TotalEffectif = 4
        });

        await svc.GeneratePlanningFromConfigAsync(new GeneratePlanningFromConfigDto
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeeklyPlanningId = planning.Id
        });

        var snapshot = await db.SubServiceShiftConfigs
            .Where(c => c.SubServiceId == 1 && !c.IsTemplate)
            .ToListAsync();
        var rows = await db.ShiftAssignments
            .Where(a => a.WeeklyPlanningId == planning.Id && !a.IsSaturday && !a.IsOnLeave && !a.IsHoliday)
            .ToListAsync();

        for (var d = 0; d < 5; d++)
        {
            var date = monday.AddDays(d);
            var day = rows.Where(a => a.AssignedDate == date).ToList();
            foreach (var cfg in snapshot.Where(c => c.RequiredCount > 0))
            {
                var assigned = day.Count(a => a.SubServiceShiftConfigId == cfg.Id);
                Assert.True(assigned >= cfg.RequiredCount, $"{date} {cfg.Label}: {assigned} < {cfg.RequiredCount}");
            }
        }

        AssertNoTripleSameShift(rows);
        AssertSameLevelGapAtMostOne(rows, new[] { 1, 2, 3, 4 });
        AssertNoConsecutiveWorked(rows);
    }

    [Fact]
    public async Task Generate_six_agents_four_shifts_fills_quotas_and_caps_successive()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Users.AddRange(
            MakeUser(1, "e1@test.ma", 3),
            MakeUser(2, "c1@test.ma", 2),
            MakeUser(3, "c2@test.ma", 2),
            MakeUser(4, "d1@test.ma", 1),
            MakeUser(5, "e2@test.ma", 3),
            MakeUser(6, "e3@test.ma", 3));
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await svc.SaveShiftTemplateAsync(new SaveShiftConfigDto
        {
            SubServiceId = 1,
            Shifts =
            [
                new ShiftConfigItemDto { Label = "8h", StartTime = "08:00", WorkHours = 8, RequiredCount = 2, DisplayOrder = 1 },
                new ShiftConfigItemDto { Label = "9h", StartTime = "09:00", WorkHours = 8, RequiredCount = 1, DisplayOrder = 2 },
                new ShiftConfigItemDto { Label = "10h", StartTime = "10:00", WorkHours = 8, RequiredCount = 1, DisplayOrder = 3 },
                new ShiftConfigItemDto { Label = "11h", StartTime = "11:00", WorkHours = 8, RequiredCount = 1, DisplayOrder = 4 },
            ]
        });

        var monday = new DateOnly(2026, 7, 20);
        var planning = await svc.CreatePlanningAsync(new CreateWeeklyPlanningDto
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeekStartDate = monday,
            TotalEffectif = 6
        });

        await svc.GeneratePlanningFromConfigAsync(new GeneratePlanningFromConfigDto
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeeklyPlanningId = planning.Id
        });

        var snapshot = await db.SubServiceShiftConfigs
            .Where(c => c.SubServiceId == 1 && !c.IsTemplate)
            .ToListAsync();
        var rows = await db.ShiftAssignments
            .Where(a => a.WeeklyPlanningId == planning.Id && !a.IsSaturday && !a.IsOnLeave && !a.IsHoliday)
            .ToListAsync();

        for (var d = 0; d < 5; d++)
        {
            var date = monday.AddDays(d);
            var day = rows.Where(a => a.AssignedDate == date).ToList();
            foreach (var cfg in snapshot.Where(c => c.RequiredCount > 0))
            {
                var assigned = day.Count(a => a.SubServiceShiftConfigId == cfg.Id);
                Assert.True(assigned >= cfg.RequiredCount, $"{date} {cfg.Label}: {assigned} < {cfg.RequiredCount}");
            }
        }

        AssertMaxSuccessiveStreak(rows, 2);
    }

    [Fact]
    public async Task Generate_skips_leave_when_checking_consecutive()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Users.AddRange(
            MakeUser(1, "a@test.ma", 2),
            MakeUser(2, "b@test.ma", 2));
        db.Conges.Add(new Conge
        {
            UserId = 1,
            StartDate = new DateOnly(2026, 7, 22),
            EndDate = new DateOnly(2026, 7, 22),
            Reason = "CP",
            Status = CongeStatus.Approved
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await svc.SaveShiftTemplateAsync(new SaveShiftConfigDto
        {
            SubServiceId = 1,
            Shifts =
            [
                new ShiftConfigItemDto { Label = "8h", StartTime = "08:00", WorkHours = 8, RequiredCount = 0, DisplayOrder = 1 },
                new ShiftConfigItemDto { Label = "9h", StartTime = "09:00", WorkHours = 8, RequiredCount = 0, DisplayOrder = 2 },
                new ShiftConfigItemDto { Label = "10h", StartTime = "10:00", WorkHours = 8, RequiredCount = 0, DisplayOrder = 3 },
                new ShiftConfigItemDto { Label = "11h", StartTime = "11:00", WorkHours = 8, RequiredCount = 0, DisplayOrder = 4 },
            ]
        });

        var monday = new DateOnly(2026, 7, 20);
        var planning = await svc.CreatePlanningAsync(new CreateWeeklyPlanningDto
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeekStartDate = monday,
            TotalEffectif = 2
        });

        await svc.GeneratePlanningFromConfigAsync(new GeneratePlanningFromConfigDto
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeeklyPlanningId = planning.Id
        });

        var wed = await db.ShiftAssignments.SingleAsync(a =>
            a.WeeklyPlanningId == planning.Id && a.UserId == 1 && a.AssignedDate == monday.AddDays(2));
        Assert.True(wed.IsOnLeave);

        var rows = await db.ShiftAssignments
            .Where(a => a.WeeklyPlanningId == planning.Id && !a.IsSaturday)
            .ToListAsync();
        AssertNoConsecutiveWorked(rows);
        AssertNoTripleSameShift(rows.Where(a => !a.IsOnLeave && !a.IsHoliday).ToList());
    }

    [Fact]
    public async Task Generate_two_experts_four_shifts_gap_at_most_one()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Users.AddRange(
            MakeUser(1, "e1@test.ma", 3),
            MakeUser(2, "e2@test.ma", 3),
            MakeUser(3, "c1@test.ma", 2),
            MakeUser(4, "c2@test.ma", 2));
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await svc.SaveShiftTemplateAsync(new SaveShiftConfigDto
        {
            SubServiceId = 1,
            Shifts =
            [
                new ShiftConfigItemDto { Label = "8h", StartTime = "08:00", WorkHours = 8, RequiredCount = 1, DisplayOrder = 1 },
                new ShiftConfigItemDto { Label = "9h", StartTime = "09:00", WorkHours = 8, RequiredCount = 1, DisplayOrder = 2 },
                new ShiftConfigItemDto { Label = "10h", StartTime = "10:00", WorkHours = 8, RequiredCount = 1, DisplayOrder = 3 },
                new ShiftConfigItemDto { Label = "11h", StartTime = "11:00", WorkHours = 8, RequiredCount = 1, DisplayOrder = 4 },
            ]
        });

        var monday = new DateOnly(2026, 7, 20);
        var planning = await svc.CreatePlanningAsync(new CreateWeeklyPlanningDto
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeekStartDate = monday,
            TotalEffectif = 4
        });

        await svc.GeneratePlanningFromConfigAsync(new GeneratePlanningFromConfigDto
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeeklyPlanningId = planning.Id
        });

        var rows = await db.ShiftAssignments
            .Where(a => a.WeeklyPlanningId == planning.Id && !a.IsSaturday && !a.IsOnLeave && !a.IsHoliday)
            .ToListAsync();

        AssertNoTripleSameShift(rows);
        AssertSameLevelGapAtMostOne(rows, new[] { 1, 2 });
        AssertSameLevelGapAtMostOne(rows, new[] { 3, 4 });
        AssertNoConsecutiveWorked(rows);
    }

    [Fact]
    public async Task Generate_five_agents_seniors_fair_no_triple()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Users.AddRange(
            MakeUser(1, "d@test.ma", 1),
            MakeUser(2, "c1@test.ma", 2),
            MakeUser(3, "c2@test.ma", 2),
            MakeUser(4, "e1@test.ma", 3),
            MakeUser(5, "e2@test.ma", 3));
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await svc.SaveShiftTemplateAsync(new SaveShiftConfigDto
        {
            SubServiceId = 1,
            Shifts =
            [
                new ShiftConfigItemDto { Label = "8h", StartTime = "08:00", WorkHours = 8, RequiredCount = 2, DisplayOrder = 1 },
                new ShiftConfigItemDto { Label = "9h", StartTime = "09:00", WorkHours = 8, RequiredCount = 1, DisplayOrder = 2 },
                new ShiftConfigItemDto { Label = "10h", StartTime = "10:00", WorkHours = 8, RequiredCount = 1, DisplayOrder = 3 },
                new ShiftConfigItemDto { Label = "11h", StartTime = "11:00", WorkHours = 8, RequiredCount = 1, DisplayOrder = 4 },
            ]
        });

        var monday = new DateOnly(2026, 7, 20);
        var planning = await svc.CreatePlanningAsync(new CreateWeeklyPlanningDto
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeekStartDate = monday,
            TotalEffectif = 5
        });

        await svc.GeneratePlanningFromConfigAsync(new GeneratePlanningFromConfigDto
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeeklyPlanningId = planning.Id
        });

        var rows = await db.ShiftAssignments
            .Where(a => a.WeeklyPlanningId == planning.Id && !a.IsSaturday && !a.IsOnLeave && !a.IsHoliday)
            .ToListAsync();

        var seniors = rows.Where(a => a.UserId != 1).ToList();
        AssertNoTripleSameShift(seniors);
        AssertSameLevelGapAtMostOne(rows, new[] { 2, 3 });
        AssertSameLevelGapAtMostOne(rows, new[] { 4, 5 });
        AssertNoConsecutiveWorked(seniors);
    }

    private static void AssertNoTripleSameShift(List<ShiftAssignment> weekdayWorked)
    {
        foreach (var g in weekdayWorked.GroupBy(a => a.UserId))
        {
            foreach (var byShift in g.GroupBy(a => a.SubServiceShiftConfigId))
                Assert.True(byShift.Count() <= 2, $"user {g.Key} shift {byShift.Key} x{byShift.Count()}");
        }
    }

    private static void AssertSameLevelGapAtMostOne(List<ShiftAssignment> weekdayWorked, int[] userIds)
    {
        var shiftIds = weekdayWorked
            .Where(a => a.SubServiceShiftConfigId != null)
            .Select(a => a.SubServiceShiftConfigId!.Value)
            .Distinct();
        foreach (var sid in shiftIds)
        {
            var counts = userIds
                .Select(uid => weekdayWorked.Count(a => a.UserId == uid && a.SubServiceShiftConfigId == sid))
                .ToList();
            Assert.True(counts.Max() - counts.Min() <= 1, $"shift {sid} counts {string.Join(",", counts)}");
        }
    }

    private static void AssertMaxSuccessiveStreak(List<ShiftAssignment> rows, int maxStreak)
    {
        foreach (var g in rows.GroupBy(a => a.UserId))
        {
            var worked = g
                .Where(a => !a.IsOnLeave && !a.IsHoliday && a.SubServiceShiftConfigId != null)
                .OrderBy(a => a.AssignedDate)
                .ToList();
            var streak = 1;
            for (var i = 1; i < worked.Count; i++)
            {
                if (worked[i].SubServiceShiftConfigId == worked[i - 1].SubServiceShiftConfigId)
                    streak++;
                else
                    streak = 1;
                Assert.True(streak <= maxStreak, $"user {g.Key} successive x{streak} on {worked[i].SubServiceShiftConfigId}");
            }
        }
    }

    private static void AssertNoConsecutiveWorked(List<ShiftAssignment> rows)
    {
        foreach (var g in rows.GroupBy(a => a.UserId))
        {
            var worked = g
                .Where(a => !a.IsOnLeave && !a.IsHoliday && a.SubServiceShiftConfigId != null)
                .OrderBy(a => a.AssignedDate)
                .ToList();
            for (var i = 1; i < worked.Count; i++)
            {
                Assert.NotEqual(worked[i - 1].SubServiceShiftConfigId, worked[i].SubServiceShiftConfigId);
            }
        }
    }

    private sealed class FakePlanningHubContext : IHubContext<PlanningHub>
    {
        public IHubClients Clients { get; } = new FakeClients();
        public IGroupManager Groups { get; } = new FakeGroups();

        private sealed class FakeClients : IHubClients
        {
            public IClientProxy All => new FakeProxy();
            public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new FakeProxy();
            public IClientProxy Client(string connectionId) => new FakeProxy();
            public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new FakeProxy();
            public IClientProxy Group(string groupName) => new FakeProxy();
            public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new FakeProxy();
            public IClientProxy Groups(IReadOnlyList<string> groupNames) => new FakeProxy();
            public IClientProxy User(string userId) => new FakeProxy();
            public IClientProxy Users(IReadOnlyList<string> userIds) => new FakeProxy();
        }

        private sealed class FakeGroups : IGroupManager
        {
            public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
                => Task.CompletedTask;
            public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }

        private sealed class FakeProxy : IClientProxy
        {
            public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }
    }
}
