using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Planning.Application.DTOs.Planning;
using Planning.Application.Exceptions;
using Planning.Domain.Entities;
using Planning.Domain.Enums;
using Planning.Infrastructure.Hubs;
using Planning.Infrastructure.Persistence;
using Planning.Infrastructure.Services;

namespace Planning.UnitTests;

public class WeeklyShiftModeCarryOverTests
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

    private static DateOnly MondayOf(DateOnly d)
    {
        var diff = ((int)d.DayOfWeek + 6) % 7;
        return d.AddDays(-diff);
    }

    private static string WeekCode(DateOnly monday)
    {
        var dt = monday.ToDateTime(TimeOnly.MinValue);
        return $"{ISOWeek.GetYear(dt)}-W{ISOWeek.GetWeekOfYear(dt):D2}";
    }

    private static async Task SeedBaseAsync(AppDbContext db)
    {
        db.Roles.Add(new Role { Id = 1, Name = "Pilote" });
        db.Floors.Add(new Floor { Id = 1, Name = "Floor 1", FloorNumber = 1 });
        db.Services.Add(new Service { Id = 1, FloorId = 1, Name = "Svc", Code = "S1" });
        db.SubServices.Add(new SubService { Id = 1, ServiceId = 1, Name = "Cellule A", Code = "CA" });
        db.PlanningAutoGenerateSettings.Add(new PlanningAutoGenerateSettings());
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

    private static async Task SeedMultiModeTemplateAsync(PlanningService svc)
    {
        await svc.SaveShiftTemplateAsync(new SaveShiftConfigDto
        {
            SubServiceId = 1,
            MultiShiftModesEnabled = true,
            Modes =
            [
                new ShiftModeProfileSaveDto
                {
                    Title = "emails",
                    DisplayOrder = 1,
                    IsDefault = true,
                    IsActive = true,
                    MinPresencePercent = 70,
                    Shifts =
                    [
                        new ShiftConfigItemDto
                        {
                            Label = "8h", StartTime = "08:00", WorkHours = 8,
                            Percentage = 50, DisplayOrder = 1
                        },
                        new ShiftConfigItemDto
                        {
                            Label = "11h", StartTime = "11:00", WorkHours = 8,
                            Percentage = 50, DisplayOrder = 2
                        },
                    ]
                },
                new ShiftModeProfileSaveDto
                {
                    Title = "BTE",
                    DisplayOrder = 2,
                    IsDefault = false,
                    IsActive = true,
                    MinPresencePercent = 70,
                    Shifts =
                    [
                        new ShiftConfigItemDto
                        {
                            Label = "9h", StartTime = "09:00", WorkHours = 8,
                            Percentage = 100, DisplayOrder = 1
                        },
                    ]
                },
            ]
        });
    }

    [Fact]
    public void Deadline_tuesday_gen_is_monday_2359()
    {
        var settings = new PlanningAutoGenerateSettings { DayOfWeek = (int)DayOfWeek.Tuesday };
        var targetMonday = new DateOnly(2026, 9, 7);
        var deadline = ShiftModePlanDeadline.ComputeCarryDeadlineLocal(settings, targetMonday);
        Assert.Equal(new DateOnly(2026, 8, 31), DateOnly.FromDateTime(deadline));
        Assert.Equal(23, deadline.Hour);
        Assert.Equal(59, deadline.Minute);
        var gen = ShiftModePlanDeadline.ComputeGenerationDate(settings, targetMonday);
        Assert.Equal(new DateOnly(2026, 9, 1), gen);
    }

    [Fact]
    public void Deadline_thursday_gen_is_wednesday_2359()
    {
        var settings = new PlanningAutoGenerateSettings { DayOfWeek = (int)DayOfWeek.Thursday };
        var targetMonday = new DateOnly(2026, 9, 7);
        var deadline = ShiftModePlanDeadline.ComputeCarryDeadlineLocal(settings, targetMonday);
        Assert.Equal(new DateOnly(2026, 9, 2), DateOnly.FromDateTime(deadline));
    }

    [Fact]
    public void ShouldBlock_future_week_before_deadline()
    {
        var settings = new PlanningAutoGenerateSettings { DayOfWeek = (int)DayOfWeek.Tuesday };
        var target = new DateOnly(2026, 9, 7);
        var mondayMorning = new DateTime(2026, 8, 31, 10, 0, 0);
        Assert.True(ShiftModePlanDeadline.ShouldBlockUntilSupervisorSave(
            settings, target, new DateOnly(2026, 8, 31), mondayMorning));
    }

    [Fact]
    public void ShouldNotBlock_current_week()
    {
        var settings = new PlanningAutoGenerateSettings { DayOfWeek = (int)DayOfWeek.Tuesday };
        var currentMonday = new DateOnly(2026, 8, 31);
        Assert.False(ShiftModePlanDeadline.ShouldBlockUntilSupervisorSave(
            settings, currentMonday, currentMonday, new DateTime(2026, 9, 1, 10, 0, 0)));
    }

    [Fact]
    public async Task Get_prefers_current_week_plan_over_older_when_target_has_none()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Users.AddRange(MakeUser(1, "a@t.ma", 2), MakeUser(2, "b@t.ma", 2));
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await SeedMultiModeTemplateAsync(svc);

        var modes = await db.ShiftModeProfiles.OrderBy(p => p.DisplayOrder).ToListAsync();
        var emailsId = modes.First(m => m.Title == "emails").Id;
        var bteId = modes.First(m => m.Title == "BTE").Id;

        var currentMonday = MondayOf(DateOnly.FromDateTime(DateTime.Today));
        var olderMonday = currentMonday.AddDays(-14);

        db.WeeklyCellShiftModePlans.Add(new WeeklyCellShiftModePlan
        {
            SubServiceId = 1,
            WeekCode = WeekCode(olderMonday),
            WeekStartDate = olderMonday,
            IsValidated = true,
            ValidatedAt = DateTime.UtcNow,
            EmployeeModes =
            [
                new WeeklyEmployeeShiftMode { UserId = 1, ShiftModeProfileId = emailsId },
                new WeeklyEmployeeShiftMode { UserId = 2, ShiftModeProfileId = emailsId },
            ]
        });
        db.WeeklyCellShiftModePlans.Add(new WeeklyCellShiftModePlan
        {
            SubServiceId = 1,
            WeekCode = WeekCode(currentMonday),
            WeekStartDate = currentMonday,
            IsValidated = true,
            ValidatedAt = DateTime.UtcNow,
            EmployeeModes =
            [
                new WeeklyEmployeeShiftMode { UserId = 1, ShiftModeProfileId = bteId },
                new WeeklyEmployeeShiftMode { UserId = 2, ShiftModeProfileId = emailsId },
            ]
        });
        await db.SaveChangesAsync();

        var nextMonday = currentMonday.AddDays(7);
        var plan = await svc.GetWeeklyShiftModePlanAsync(1, WeekCode(nextMonday), nextMonday);

        Assert.True(plan.IsCopiedPreview);
        Assert.Equal(WeekCode(currentMonday), plan.SourceWeekCode);
        Assert.Equal(bteId, plan.Employees.Single(e => e.UserId == 1).ShiftModeProfileId);
        Assert.Equal(emailsId, plan.Employees.Single(e => e.UserId == 2).ShiftModeProfileId);
    }

    [Fact]
    public async Task Get_without_plan_prefills_previous_week_and_default_for_newcomer()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Users.AddRange(MakeUser(1, "a@t.ma", 2), MakeUser(2, "b@t.ma", 2));
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await SeedMultiModeTemplateAsync(svc);

        var modes = await db.ShiftModeProfiles.OrderBy(p => p.DisplayOrder).ToListAsync();
        var emailsId = modes.First(m => m.Title == "emails").Id;
        var bteId = modes.First(m => m.Title == "BTE").Id;

        db.Users.Add(MakeUser(3, "c@t.ma", 2));
        var prevMonday = new DateOnly(2026, 7, 20);
        db.WeeklyCellShiftModePlans.Add(new WeeklyCellShiftModePlan
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeekStartDate = prevMonday,
            IsValidated = true,
            ValidatedAt = DateTime.UtcNow,
            EmployeeModes =
            [
                new WeeklyEmployeeShiftMode { UserId = 1, ShiftModeProfileId = bteId },
                new WeeklyEmployeeShiftMode { UserId = 2, ShiftModeProfileId = emailsId },
            ]
        });
        await db.SaveChangesAsync();

        var nextMonday = MondayOf(DateOnly.FromDateTime(DateTime.Today)).AddDays(7);
        var plan = await svc.GetWeeklyShiftModePlanAsync(1, WeekCode(nextMonday), nextMonday);

        Assert.True(plan.IsCopiedPreview);
        Assert.Equal("2026-W30", plan.SourceWeekCode);
        Assert.False(plan.IsSupervisorSaved);
        Assert.NotNull(plan.DeadlineLocal);
        Assert.Equal(bteId, plan.Employees.Single(e => e.UserId == 1).ShiftModeProfileId);
        Assert.Equal(emailsId, plan.Employees.Single(e => e.UserId == 2).ShiftModeProfileId);
        Assert.Equal(emailsId, plan.Employees.Single(e => e.UserId == 3).ShiftModeProfileId);
        Assert.Equal(0, await db.WeeklyCellShiftModePlans.CountAsync(p => p.WeekCode == plan.WeekCode));
    }

    [Fact]
    public async Task Get_without_mode_plan_prefills_from_previous_generated_planning()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Users.AddRange(MakeUser(1, "a@t.ma", 2), MakeUser(2, "b@t.ma", 2));
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await SeedMultiModeTemplateAsync(svc);

        var modes = await db.ShiftModeProfiles.OrderBy(p => p.DisplayOrder).ToListAsync();
        var emailsId = modes.First(m => m.Title == "emails").Id;
        var bteId = modes.First(m => m.Title == "BTE").Id;

        var prevMonday = new DateOnly(2026, 7, 20);
        db.WeeklyPlannings.Add(new WeeklyPlanning
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeekStartDate = prevMonday,
            Status = PlanningStatus.Published,
            TotalEffectif = 2
        });
        await db.SaveChangesAsync();
        var planningId = await db.WeeklyPlannings.Select(p => p.Id).SingleAsync();

        db.ShiftAssignments.AddRange(
            new ShiftAssignment
            {
                WeeklyPlanningId = planningId,
                UserId = 1,
                AssignedDate = prevMonday,
                DayOfWeek = DayOfWeekEnum.Monday,
                ShiftModeProfileId = bteId
            },
            new ShiftAssignment
            {
                WeeklyPlanningId = planningId,
                UserId = 1,
                AssignedDate = prevMonday.AddDays(1),
                DayOfWeek = DayOfWeekEnum.Tuesday,
                ShiftModeProfileId = bteId
            },
            new ShiftAssignment
            {
                WeeklyPlanningId = planningId,
                UserId = 2,
                AssignedDate = prevMonday,
                DayOfWeek = DayOfWeekEnum.Monday,
                ShiftModeProfileId = emailsId
            });
        await db.SaveChangesAsync();

        var nextMonday = MondayOf(DateOnly.FromDateTime(DateTime.Today)).AddDays(7);
        var plan = await svc.GetWeeklyShiftModePlanAsync(1, WeekCode(nextMonday), nextMonday);

        Assert.True(plan.IsCopiedPreview);
        Assert.Equal("2026-W30", plan.SourceWeekCode);
        Assert.Equal(bteId, plan.Employees.Single(e => e.UserId == 1).ShiftModeProfileId);
        Assert.Equal(emailsId, plan.Employees.Single(e => e.UserId == 2).ShiftModeProfileId);
    }

    [Fact]
    public async Task Get_from_planning_newcomer_uses_default_mode()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Users.Add(MakeUser(1, "a@t.ma", 2));
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await SeedMultiModeTemplateAsync(svc);
        var emailsId = await db.ShiftModeProfiles.Where(p => p.Title == "emails").Select(p => p.Id).SingleAsync();
        var bteId = await db.ShiftModeProfiles.Where(p => p.Title == "BTE").Select(p => p.Id).SingleAsync();

        var prevMonday = new DateOnly(2026, 7, 20);
        db.WeeklyPlannings.Add(new WeeklyPlanning
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeekStartDate = prevMonday,
            Status = PlanningStatus.Published
        });
        await db.SaveChangesAsync();
        var planningId = await db.WeeklyPlannings.Select(p => p.Id).SingleAsync();
        db.ShiftAssignments.Add(new ShiftAssignment
        {
            WeeklyPlanningId = planningId,
            UserId = 1,
            AssignedDate = prevMonday,
            DayOfWeek = DayOfWeekEnum.Monday,
            ShiftModeProfileId = bteId
        });
        db.Users.Add(MakeUser(2, "b@t.ma", 2));
        await db.SaveChangesAsync();

        var nextMonday = MondayOf(DateOnly.FromDateTime(DateTime.Today)).AddDays(7);
        var plan = await svc.GetWeeklyShiftModePlanAsync(1, WeekCode(nextMonday), nextMonday);

        Assert.Equal(bteId, plan.Employees.Single(e => e.UserId == 1).ShiftModeProfileId);
        Assert.Equal(emailsId, plan.Employees.Single(e => e.UserId == 2).ShiftModeProfileId);
    }

    [Fact]
    public async Task Generate_next_week_blocked_before_deadline_without_save()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Users.Add(MakeUser(1, "a@t.ma", 2));
        var settings = await db.PlanningAutoGenerateSettings.SingleAsync();
        settings.DayOfWeek = (int)DayOfWeek.Saturday;
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await SeedMultiModeTemplateAsync(svc);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var targetMonday = MondayOf(today).AddDays(7);
        var nowLocal = DateTime.Now;
        if (!ShiftModePlanDeadline.ShouldBlockUntilSupervisorSave(settings, targetMonday, today, nowLocal))
            targetMonday = targetMonday.AddDays(7);
        var planning = await svc.CreatePlanningAsync(new CreateWeeklyPlanningDto
        {
            SubServiceId = 1,
            WeekCode = WeekCode(targetMonday),
            WeekStartDate = targetMonday,
            TotalEffectif = 1
        });

        var ex = await Assert.ThrowsAsync<SupervisorModesPendingException>(() =>
            svc.GeneratePlanningFromConfigAsync(new GeneratePlanningFromConfigDto
            {
                SubServiceId = 1,
                WeekCode = WeekCode(targetMonday),
                WeeklyPlanningId = planning.Id
            }));
        Assert.Contains("superviseur", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("deadline", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Generate_next_week_after_save_does_not_change_current_week_assignments()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Users.AddRange(MakeUser(1, "a@t.ma", 2), MakeUser(2, "b@t.ma", 2));
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await SeedMultiModeTemplateAsync(svc);
        var modes = await db.ShiftModeProfiles.OrderBy(p => p.DisplayOrder).ToListAsync();
        var emailsId = modes.First(m => m.Title == "emails").Id;
        var bteId = modes.First(m => m.Title == "BTE").Id;

        var currentMonday = new DateOnly(2026, 7, 20);
        db.WeeklyCellShiftModePlans.Add(new WeeklyCellShiftModePlan
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeekStartDate = currentMonday,
            IsValidated = true,
            ValidatedAt = DateTime.UtcNow,
            EmployeeModes =
            [
                new WeeklyEmployeeShiftMode { UserId = 1, ShiftModeProfileId = emailsId },
                new WeeklyEmployeeShiftMode { UserId = 2, ShiftModeProfileId = emailsId },
            ]
        });
        await db.SaveChangesAsync();

        var currentPlanning = await svc.CreatePlanningAsync(new CreateWeeklyPlanningDto
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeekStartDate = currentMonday,
            TotalEffectif = 2
        });
        await svc.GeneratePlanningFromConfigAsync(new GeneratePlanningFromConfigDto
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeeklyPlanningId = currentPlanning.Id
        });

        var before = await db.ShiftAssignments
            .AsNoTracking()
            .Where(a => a.WeeklyPlanningId == currentPlanning.Id)
            .Select(a => new { a.UserId, a.AssignedDate, a.SubServiceShiftConfigId, a.ShiftModeProfileId })
            .OrderBy(a => a.UserId).ThenBy(a => a.AssignedDate)
            .ToListAsync();

        var nextMonday = MondayOf(DateOnly.FromDateTime(DateTime.Today)).AddDays(7);
        await svc.SaveWeeklyShiftModePlanAsync(new SaveWeeklyShiftModePlanDto
        {
            SubServiceId = 1,
            WeekCode = WeekCode(nextMonday),
            WeekStartDate = nextMonday,
            Employees =
            [
                new WeeklyEmployeeShiftModeItemDto { UserId = 1, ShiftModeProfileId = bteId },
                new WeeklyEmployeeShiftModeItemDto { UserId = 2, ShiftModeProfileId = bteId },
            ]
        });

        var nextPlanning = await svc.CreatePlanningAsync(new CreateWeeklyPlanningDto
        {
            SubServiceId = 1,
            WeekCode = WeekCode(nextMonday),
            WeekStartDate = nextMonday,
            TotalEffectif = 2
        });
        await svc.GeneratePlanningFromConfigAsync(new GeneratePlanningFromConfigDto
        {
            SubServiceId = 1,
            WeekCode = WeekCode(nextMonday),
            WeeklyPlanningId = nextPlanning.Id
        });

        var after = await db.ShiftAssignments
            .AsNoTracking()
            .Where(a => a.WeeklyPlanningId == currentPlanning.Id)
            .Select(a => new { a.UserId, a.AssignedDate, a.SubServiceShiftConfigId, a.ShiftModeProfileId })
            .OrderBy(a => a.UserId).ThenBy(a => a.AssignedDate)
            .ToListAsync();
        Assert.Equal(before, after);

        var nextModes = await db.ShiftAssignments
            .Where(a => a.WeeklyPlanningId == nextPlanning.Id && a.ShiftModeProfileId != null)
            .Select(a => a.ShiftModeProfileId!.Value)
            .Distinct()
            .ToListAsync();
        Assert.All(nextModes, id => Assert.Equal(bteId, id));
    }

    [Fact]
    public async Task Generate_past_week_without_saved_plan_carries_over_previous()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Users.Add(MakeUser(1, "a@t.ma", 2));
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await SeedMultiModeTemplateAsync(svc);
        var emailsId = (await db.ShiftModeProfiles.SingleAsync(p => p.Title == "emails")).Id;

        db.WeeklyCellShiftModePlans.Add(new WeeklyCellShiftModePlan
        {
            SubServiceId = 1,
            WeekCode = "2026-W29",
            WeekStartDate = new DateOnly(2026, 7, 13),
            IsValidated = true,
            ValidatedAt = DateTime.UtcNow,
            EmployeeModes =
            [
                new WeeklyEmployeeShiftMode { UserId = 1, ShiftModeProfileId = emailsId },
            ]
        });
        await db.SaveChangesAsync();

        var planning = await svc.CreatePlanningAsync(new CreateWeeklyPlanningDto
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeekStartDate = new DateOnly(2026, 7, 20),
            TotalEffectif = 1
        });
        await svc.GeneratePlanningFromConfigAsync(new GeneratePlanningFromConfigDto
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeeklyPlanningId = planning.Id
        });

        var carried = await db.WeeklyCellShiftModePlans
            .Include(p => p.EmployeeModes)
            .SingleAsync(p => p.WeekCode == "2026-W30");
        Assert.True(carried.IsValidated);
        Assert.Null(carried.ValidatedByUserId);
        Assert.Equal(emailsId, carried.EmployeeModes.Single().ShiftModeProfileId);
        Assert.True(await db.ShiftAssignments.AnyAsync(a => a.WeeklyPlanningId == planning.Id));
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
