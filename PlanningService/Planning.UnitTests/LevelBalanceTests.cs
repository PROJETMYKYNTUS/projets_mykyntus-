using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Planning.Application.DTOs.Planning;
using Planning.Domain.Entities;
using Planning.Domain.Enums;
using Planning.Infrastructure.Hubs;
using Planning.Infrastructure.Persistence;
using Planning.Infrastructure.Services;

namespace Planning.UnitTests;

public class LevelBalanceTests
{
    private static AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    private static PlanningService CreateService(AppDbContext db) =>
        new(db, new FakePlanningHubContext());

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

    [Fact]
    public void ApplyShiftKinds_earliest_opening_latest_closing()
    {
        var configs = new List<SubServiceShiftConfig>
        {
            new() { Label = "8h", StartTime = new TimeOnly(8, 0), DisplayOrder = 1 },
            new() { Label = "9h", StartTime = new TimeOnly(9, 0), DisplayOrder = 2 },
            new() { Label = "10h", StartTime = new TimeOnly(10, 0), DisplayOrder = 3 },
        };
        LevelBalanceEvaluator.ApplyShiftKindsFromStartTimes(configs);
        Assert.Equal(ShiftKind.Opening, configs[0].ShiftKind);
        Assert.Equal(ShiftKind.Standard, configs[1].ShiftKind);
        Assert.Equal(ShiftKind.Closing, configs[2].ShiftKind);
    }

    [Fact]
    public void Evaluate_beginner_alone_on_opening_is_anomaly()
    {
        var cfg = new SubServiceShiftConfig
        {
            Id = 1, Label = "8h", StartTime = new TimeOnly(8, 0), ShiftKind = ShiftKind.Opening
        };
        var monday = new DateOnly(2026, 7, 20);
        var assignments = new List<ShiftAssignment>
        {
            new()
            {
                UserId = 1,
                AssignedDate = monday,
                SubServiceShiftConfigId = 1,
                IsOnLeave = false,
                IsHoliday = false
            }
        };
        var users = new Dictionary<int, User>
        {
            [1] = MakeUser(1, "b@t.ma", 1)
        };

        var anomalies = LevelBalanceEvaluator.Evaluate(assignments, [cfg], users);
        Assert.Single(anomalies);
        Assert.Equal("LEVEL_BALANCE", anomalies[0].Code);
        Assert.True(anomalies[0].IsForced);
        Assert.Equal("Warning", anomalies[0].Severity);
    }

    [Fact]
    public void Evaluate_beginner_plus_confirmed_on_opening_ok()
    {
        var cfg = new SubServiceShiftConfig
        {
            Id = 1, Label = "8h", StartTime = new TimeOnly(8, 0), ShiftKind = ShiftKind.Opening
        };
        var monday = new DateOnly(2026, 7, 20);
        var assignments = new List<ShiftAssignment>
        {
            new() { UserId = 1, AssignedDate = monday, SubServiceShiftConfigId = 1 },
            new() { UserId = 2, AssignedDate = monday, SubServiceShiftConfigId = 1 },
        };
        var users = new Dictionary<int, User>
        {
            [1] = MakeUser(1, "b@t.ma", 1),
            [2] = MakeUser(2, "c@t.ma", 2),
        };

        var anomalies = LevelBalanceEvaluator.Evaluate(assignments, [cfg], users);
        Assert.Empty(anomalies);
    }

    [Fact]
    public void Evaluate_beginner_alone_on_standard_weekday_is_anomaly()
    {
        var cfg = new SubServiceShiftConfig
        {
            Id = 1, Label = "9h", StartTime = new TimeOnly(9, 0), ShiftKind = ShiftKind.Standard
        };
        var tuesday = new DateOnly(2026, 7, 21);
        var assignments = new List<ShiftAssignment>
        {
            new() { UserId = 1, AssignedDate = tuesday, SubServiceShiftConfigId = 1 }
        };
        var users = new Dictionary<int, User> { [1] = MakeUser(1, "b@t.ma", 1) };

        Assert.Single(LevelBalanceEvaluator.Evaluate(assignments, [cfg], users));
    }

    [Fact]
    public void Evaluate_beginner_alone_on_saturday_is_anomaly()
    {
        var cfg = new SubServiceShiftConfig
        {
            Id = 1, Label = "8h", StartTime = new TimeOnly(8, 0), ShiftKind = ShiftKind.Opening
        };
        var saturday = new DateOnly(2026, 7, 25);
        var assignments = new List<ShiftAssignment>
        {
            new()
            {
                UserId = 1,
                AssignedDate = saturday,
                SubServiceShiftConfigId = 1,
                IsSaturday = true
            }
        };
        var users = new Dictionary<int, User> { [1] = MakeUser(1, "b@t.ma", 1) };

        Assert.Single(LevelBalanceEvaluator.Evaluate(assignments, [cfg], users));
    }

    [Fact]
    public void Evaluate_saturday_ok_when_confirmed_present_any_slot()
    {
        var cfg1 = new SubServiceShiftConfig
        {
            Id = 1, Label = "8h", StartTime = new TimeOnly(8, 0), ShiftKind = ShiftKind.Opening
        };
        var cfg2 = new SubServiceShiftConfig
        {
            Id = 2, Label = "10h", StartTime = new TimeOnly(10, 0), ShiftKind = ShiftKind.Closing
        };
        var saturday = new DateOnly(2026, 7, 25);
        var assignments = new List<ShiftAssignment>
        {
            new() { UserId = 1, AssignedDate = saturday, SubServiceShiftConfigId = 1, IsSaturday = true },
            new() { UserId = 2, AssignedDate = saturday, SubServiceShiftConfigId = 2, IsSaturday = true },
        };
        var users = new Dictionary<int, User>
        {
            [1] = MakeUser(1, "b@t.ma", 1),
            [2] = MakeUser(2, "c@t.ma", 2),
        };

        Assert.Empty(LevelBalanceEvaluator.Evaluate(assignments, [cfg1, cfg2], users));
    }

    [Fact]
    public void Repair_moves_senior_onto_deficient_standard_shift()
    {
        var monday = new DateOnly(2026, 7, 20);
        var cfgStd = new SubServiceShiftConfig
        {
            Id = 1, Label = "9h", StartTime = new TimeOnly(9, 0), ShiftKind = ShiftKind.Standard, DisplayOrder = 1
        };
        var cfgClose = new SubServiceShiftConfig
        {
            Id = 2, Label = "11h", StartTime = new TimeOnly(11, 0), ShiftKind = ShiftKind.Closing, DisplayOrder = 2
        };
        var users = new Dictionary<int, User>
        {
            [1] = MakeUser(1, "b@t.ma", 1),
            [2] = MakeUser(2, "c@t.ma", 2),
        };
        var planning = new WeeklyPlanning { Id = 10, WeekStartDate = monday, SubServiceId = 1 };
        var assignments = new List<ShiftAssignment>
        {
            new()
            {
                WeeklyPlanningId = 10, UserId = 1, AssignedDate = monday,
                SubServiceShiftConfigId = 1, IsOnLeave = false, IsHoliday = false
            },
            new()
            {
                WeeklyPlanningId = 10, UserId = 2, AssignedDate = monday,
                SubServiceShiftConfigId = 2, IsOnLeave = false, IsHoliday = false
            },
        };

        LevelBalanceRepairer.Repair(
            assignments, [cfgStd, cfgClose], users, users.Values.ToList(), planning);

        Assert.Empty(LevelBalanceEvaluator.Evaluate(assignments, [cfgStd, cfgClose], users));
        Assert.Equal(2, assignments.Count(a => a.SubServiceShiftConfigId == 1));
        Assert.Contains(assignments, a => a.UserId == 2 && a.SubServiceShiftConfigId == 1);
        Assert.Contains(assignments, a => a.UserId == 1 && a.SubServiceShiftConfigId == 1);
    }

    [Fact]
    public void Repair_moves_alone_beginner_from_opening_to_middle_when_no_senior()
    {
        var monday = new DateOnly(2026, 7, 20);
        var cfgOpen = new SubServiceShiftConfig
        {
            Id = 1, Label = "8h", StartTime = new TimeOnly(8, 0), ShiftKind = ShiftKind.Opening, DisplayOrder = 1
        };
        var cfgStd = new SubServiceShiftConfig
        {
            Id = 2, Label = "9h", StartTime = new TimeOnly(9, 0), ShiftKind = ShiftKind.Standard, DisplayOrder = 2
        };
        var cfgClose = new SubServiceShiftConfig
        {
            Id = 3, Label = "11h", StartTime = new TimeOnly(11, 0), ShiftKind = ShiftKind.Closing, DisplayOrder = 3
        };
        var users = new Dictionary<int, User>
        {
            [1] = MakeUser(1, "b@t.ma", 1),
        };
        var planning = new WeeklyPlanning { Id = 10, WeekStartDate = monday, SubServiceId = 1 };
        var assignments = new List<ShiftAssignment>
        {
            new()
            {
                WeeklyPlanningId = 10, UserId = 1, AssignedDate = monday,
                SubServiceShiftConfigId = 1, IsOnLeave = false, IsHoliday = false
            },
        };

        LevelBalanceRepairer.Repair(
            assignments, [cfgOpen, cfgStd, cfgClose], users, users.Values.ToList(), planning);

        Assert.Equal(2, assignments[0].SubServiceShiftConfigId);
        var anomalies = LevelBalanceEvaluator.Evaluate(
            assignments, [cfgOpen, cfgStd, cfgClose], users, users.Values.ToList());
        Assert.Single(anomalies);
        Assert.Equal(2, anomalies[0].ShiftConfigId);
        Assert.Contains("plateau", anomalies[0].Message);
    }

    [Fact]
    public void Repair_moves_alone_beginner_from_closing_to_middle_when_no_senior()
    {
        var monday = new DateOnly(2026, 7, 20);
        var cfgOpen = new SubServiceShiftConfig
        {
            Id = 1, Label = "8h", StartTime = new TimeOnly(8, 0), ShiftKind = ShiftKind.Opening, DisplayOrder = 1
        };
        var cfgStd = new SubServiceShiftConfig
        {
            Id = 2, Label = "9h", StartTime = new TimeOnly(9, 0), ShiftKind = ShiftKind.Standard, DisplayOrder = 2
        };
        var cfgClose = new SubServiceShiftConfig
        {
            Id = 3, Label = "11h", StartTime = new TimeOnly(11, 0), ShiftKind = ShiftKind.Closing, DisplayOrder = 3
        };
        var users = new Dictionary<int, User>
        {
            [1] = MakeUser(1, "b@t.ma", 1),
        };
        var planning = new WeeklyPlanning { Id = 10, WeekStartDate = monday, SubServiceId = 1 };
        var assignments = new List<ShiftAssignment>
        {
            new()
            {
                WeeklyPlanningId = 10, UserId = 1, AssignedDate = monday,
                SubServiceShiftConfigId = 3, IsOnLeave = false, IsHoliday = false
            },
        };

        LevelBalanceRepairer.Repair(
            assignments, [cfgOpen, cfgStd, cfgClose], users, users.Values.ToList(), planning);

        Assert.Equal(2, assignments[0].SubServiceShiftConfigId);
    }

    [Fact]
    public void Repair_prefers_standard_donor_when_swapping_to_fix_opening()
    {
        var monday = new DateOnly(2026, 7, 20);
        var cfgOpen = new SubServiceShiftConfig
        {
            Id = 1, Label = "8h", StartTime = new TimeOnly(8, 0), ShiftKind = ShiftKind.Opening, DisplayOrder = 1
        };
        var cfgStd = new SubServiceShiftConfig
        {
            Id = 2, Label = "9h", StartTime = new TimeOnly(9, 0), ShiftKind = ShiftKind.Standard, DisplayOrder = 2
        };
        var cfgClose = new SubServiceShiftConfig
        {
            Id = 3, Label = "11h", StartTime = new TimeOnly(11, 0), ShiftKind = ShiftKind.Closing, DisplayOrder = 3
        };
        var users = new Dictionary<int, User>
        {
            [1] = MakeUser(1, "b@t.ma", 1),
            [2] = MakeUser(2, "c1@t.ma", 2),
            [3] = MakeUser(3, "c2@t.ma", 2),
            [4] = MakeUser(4, "c3@t.ma", 2),
            [5] = MakeUser(5, "c4@t.ma", 2),
        };
        var planning = new WeeklyPlanning { Id = 10, WeekStartDate = monday, SubServiceId = 1 };
        // Débutant seul en ouverture ; 2 seniors au milieu ; 2 seniors en fermeture
        var assignments = new List<ShiftAssignment>
        {
            new()
            {
                WeeklyPlanningId = 10, UserId = 1, AssignedDate = monday,
                SubServiceShiftConfigId = 1, IsOnLeave = false, IsHoliday = false
            },
            new()
            {
                WeeklyPlanningId = 10, UserId = 2, AssignedDate = monday,
                SubServiceShiftConfigId = 2, IsOnLeave = false, IsHoliday = false
            },
            new()
            {
                WeeklyPlanningId = 10, UserId = 3, AssignedDate = monday,
                SubServiceShiftConfigId = 2, IsOnLeave = false, IsHoliday = false
            },
            new()
            {
                WeeklyPlanningId = 10, UserId = 4, AssignedDate = monday,
                SubServiceShiftConfigId = 3, IsOnLeave = false, IsHoliday = false
            },
            new()
            {
                WeeklyPlanningId = 10, UserId = 5, AssignedDate = monday,
                SubServiceShiftConfigId = 3, IsOnLeave = false, IsHoliday = false
            },
        };

        LevelBalanceRepairer.Repair(
            assignments, [cfgOpen, cfgStd, cfgClose], users, users.Values.ToList(), planning);

        Assert.Empty(LevelBalanceEvaluator.Evaluate(assignments, [cfgOpen, cfgStd, cfgClose], users));
        // Le débutant doit atterrir au milieu (donneur Standard préféré)
        Assert.Equal(2, assignments.First(a => a.UserId == 1).SubServiceShiftConfigId);
        // Un senior du milieu part en ouverture
        Assert.Contains(assignments, a => a.UserId is 2 or 3 && a.SubServiceShiftConfigId == 1);
    }

    [Fact]
    public void Repair_does_not_force_off_confirmed_to_work_saturday()
    {
        var saturday = new DateOnly(2026, 7, 25);
        var monday = saturday.AddDays(-5);
        var cfg = new SubServiceShiftConfig
        {
            Id = 1, Label = "8h", StartTime = new TimeOnly(8, 0), ShiftKind = ShiftKind.Opening, DisplayOrder = 1
        };
        var beginner = MakeUser(1, "b@t.ma", 1);
        var confirmedOff = MakeUser(2, "c@t.ma", 2);
        var users = new Dictionary<int, User> { [1] = beginner, [2] = confirmedOff };
        var planning = new WeeklyPlanning
        {
            Id = 10,
            WeekStartDate = monday,
            SubServiceId = 1,
            WeekCode = "2026-W30"
        };
        // Débutant seul samedi ; Confirmé volontairement Off (pas d'assignation travaillée)
        var assignments = new List<ShiftAssignment>
        {
            new()
            {
                WeeklyPlanningId = 10,
                UserId = 1,
                AssignedDate = saturday,
                SubServiceShiftConfigId = 1,
                IsSaturday = true,
                IsHalfDaySaturday = true,
                SaturdaySlot = 1
            }
        };

        LevelBalanceRepairer.Repair(assignments, [cfg], users, users.Values.ToList(), planning);

        Assert.DoesNotContain(assignments, a => a.UserId == 2 && a.SubServiceShiftConfigId != null);
        var anomalies = LevelBalanceEvaluator.Evaluate(assignments, [cfg], users, users.Values.ToList());
        Assert.Single(anomalies);
        Assert.True(anomalies[0].IsForced);
    }

    [Fact]
    public async Task Generate_with_only_beginners_succeeds_with_forced_anomaly()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Users.Add(MakeUser(1, "b1@test.ma", level: 1));
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await svc.SaveShiftTemplateAsync(new SaveShiftConfigDto
        {
            SubServiceId = 1,
            Shifts =
            [
                new ShiftConfigItemDto { Label = "8h", StartTime = "08:00", WorkHours = 8, RequiredCount = 1, DisplayOrder = 1 },
                new ShiftConfigItemDto { Label = "10h", StartTime = "10:00", WorkHours = 8, RequiredCount = 0, DisplayOrder = 2 },
            ]
        });

        var monday = new DateOnly(2026, 7, 20);
        var planning = await svc.CreatePlanningAsync(new CreateWeeklyPlanningDto
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeekStartDate = monday,
            TotalEffectif = 1
        });

        var result = await svc.GeneratePlanningFromConfigAsync(new GeneratePlanningFromConfigDto
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeeklyPlanningId = planning.Id
        });

        Assert.NotNull(result);
        Assert.True(result.CoverageReport!.HasLevelBalanceAnomaly);
        Assert.All(result.CoverageReport.LevelBalanceAnomalies, a => Assert.True(a.IsForced));
    }

    [Fact]
    public async Task Generate_beginner_plus_confirmed_no_level_anomaly()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Users.AddRange(
            MakeUser(1, "b@test.ma", level: 1),
            MakeUser(2, "c@test.ma", level: 2));
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await svc.SaveShiftTemplateAsync(new SaveShiftConfigDto
        {
            SubServiceId = 1,
            Shifts =
            [
                new ShiftConfigItemDto { Label = "8h", StartTime = "08:00", WorkHours = 8, RequiredCount = 1, DisplayOrder = 1 },
                new ShiftConfigItemDto { Label = "10h", StartTime = "10:00", WorkHours = 8, RequiredCount = 1, DisplayOrder = 2 },
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

        var result = await svc.GeneratePlanningFromConfigAsync(new GeneratePlanningFromConfigDto
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeeklyPlanningId = planning.Id
        });

        Assert.NotNull(result);
        // Lun–Ven : le repairer doit coller Confirmé + débutant. Le samedi peut rester en anomalie
        // si le Confirmé est Off (alternance ON/OFF jamais forcée).
        Assert.DoesNotContain(
            result.CoverageReport!.LevelBalanceAnomalies,
            a => !string.Equals(a.Day, "Saturday", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(result.CoverageReport.DaySynthesis);
        Assert.True(result.CoverageReport.DaySynthesis.Count >= 6);
        Assert.Contains(result.CoverageReport.DaySynthesis[0].Shifts, s => s.BeginnerCount + s.SeniorCount > 0);
    }

    [Fact]
    public async Task Publish_allowed_when_level_anomaly_after_override()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Users.AddRange(
            MakeUser(1, "b@test.ma", level: 1),
            MakeUser(2, "c@test.ma", level: 2),
            MakeUser(3, "rh@test.ma", level: 3));
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await svc.SaveShiftTemplateAsync(new SaveShiftConfigDto
        {
            SubServiceId = 1,
            Shifts =
            [
                new ShiftConfigItemDto { Label = "8h", StartTime = "08:00", WorkHours = 8, RequiredCount = 1, DisplayOrder = 1 },
                new ShiftConfigItemDto { Label = "10h", StartTime = "10:00", WorkHours = 8, RequiredCount = 1, DisplayOrder = 2 },
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

        var generated = await svc.GeneratePlanningFromConfigAsync(new GeneratePlanningFromConfigDto
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeeklyPlanningId = planning.Id
        });

        var openingId = await db.SubServiceShiftConfigs
            .Where(c => c.WeekCode == "2026-W30" && c.ShiftKind == ShiftKind.Opening)
            .Select(c => c.Id)
            .FirstAsync();

        var mondayAssignments = await db.ShiftAssignments
            .Where(a => a.WeeklyPlanningId == generated.Id && a.AssignedDate == monday && !a.IsOnLeave)
            .Include(a => a.User)
            .ToListAsync();

        var confirmedOnOpening = mondayAssignments
            .FirstOrDefault(a => a.SubServiceShiftConfigId == openingId && a.User.Level >= 2);

        if (confirmedOnOpening != null)
        {
            await svc.OverrideShiftAsync(new OverrideShiftDto
            {
                ShiftAssignmentId = confirmedOnOpening.Id,
                NewSubServiceShiftConfigId = 0
            });
        }
        else
        {
            var beginner = mondayAssignments.First(a => a.User.Level == 1);
            await svc.OverrideShiftAsync(new OverrideShiftDto
            {
                ShiftAssignmentId = beginner.Id,
                NewSubServiceShiftConfigId = openingId
            });
            var othersOnOpening = await db.ShiftAssignments
                .Where(a => a.WeeklyPlanningId == generated.Id
                         && a.AssignedDate == monday
                         && a.SubServiceShiftConfigId == openingId
                         && a.UserId != beginner.UserId)
                .ToListAsync();
            foreach (var o in othersOnOpening)
            {
                await svc.OverrideShiftAsync(new OverrideShiftDto
                {
                    ShiftAssignmentId = o.Id,
                    NewSubServiceShiftConfigId = 0
                });
            }
        }

        await svc.RecordConsultationAsync(generated.Id, 3);
        var published = await svc.PublishPlanningAsync(generated.Id, 3);
        Assert.Equal(PlanningStatus.Published.ToString(), published.Status);
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
