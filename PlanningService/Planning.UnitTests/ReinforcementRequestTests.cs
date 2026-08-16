using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Planning.Application.DTOs.Planning;
using Planning.Domain.Entities;
using Planning.Domain.Enums;
using Planning.Infrastructure.Hubs;
using Planning.Infrastructure.Persistence;
using Planning.Infrastructure.Services;

namespace Planning.UnitTests;

/// <summary>
/// Renfort samedi : pose le shift sans toucher SaturdayHistory ;
/// éligibilité OFF / exclusion always-on ; slots ; heures programmées.
/// </summary>
public class ReinforcementRequestTests
{
    private static AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    private static PlanningReinforcementRequestService CreateService(AppDbContext db) =>
        new(db, new FakePlanningHubContext(), NullLogger<PlanningReinforcementRequestService>.Instance,
            new PlanningPerimeterResolver(db));

    /// <summary>Prochain samedi ≥ aujourd'hui (contrainte CreateAsync).</summary>
    private static DateOnly NextSaturdayOnOrAfterToday()
    {
        var d = DateOnly.FromDateTime(DateTime.Today);
        var add = ((int)DayOfWeek.Saturday - (int)d.DayOfWeek + 7) % 7;
        return d.AddDays(add);
    }

    private static string WeekCodeFromSaturday(DateOnly saturday)
    {
        var monday = saturday.AddDays(-5);
        var dt = monday.ToDateTime(TimeOnly.MinValue);
        return $"{ISOWeek.GetYear(dt)}-W{ISOWeek.GetWeekOfYear(dt):D2}";
    }

    private static string PreviousWeekCode(string weekCode)
    {
        var parts = weekCode.Split('-');
        var year = int.Parse(parts[0]);
        var week = int.Parse(parts[1].Replace("W", "", StringComparison.OrdinalIgnoreCase));
        if (week == 1) return $"{year - 1}-W52";
        return $"{year}-W{(week - 1):D2}";
    }

    private static async Task SeedBaseAsync(AppDbContext db)
    {
        db.Roles.Add(new Role { Id = 1, Name = "Admin" });
        db.Roles.Add(new Role { Id = 2, Name = "Pilote" });
        db.Floors.Add(new Floor { Id = 1, Name = "Floor 1", FloorNumber = 1 });
        db.Services.Add(new Service { Id = 1, FloorId = 1, Name = "Svc", Code = "S1" });
        db.SubServices.Add(new SubService { Id = 1, ServiceId = 1, Name = "Cellule A", Code = "CA" });
        await db.SaveChangesAsync();
    }

    private static User MakeUser(int id, string email, int level, int roleId = 2, int? saturdayMode = null) => new()
    {
        Id = id,
        FirstName = $"U{id}",
        LastName = "Test",
        Email = email,
        RoleId = roleId,
        IsActive = true,
        SubServiceId = 1,
        Level = level,
        SaturdayWorkMode = saturdayMode,
        PasswordHash = "x",
        HireDate = DateTime.UtcNow.AddYears(-1),
    };

    private static SubServiceShiftConfig MakeShift(int id, int workHours = 8, bool template = true) => new()
    {
        Id = id,
        SubServiceId = 1,
        Label = workHours <= 4 ? "4h" : "8h",
        StartTime = new TimeOnly(8, 0),
        WorkHours = workHours,
        RequiredCount = 2,
        DisplayOrder = 1,
        IsTemplate = template,
        WeekCode = template ? null : "x"
    };

    [Fact]
    public async Task Select_applies_reinforcement_shift_without_writing_SaturdayHistory()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);

        var saturday = NextSaturdayOnOrAfterToday();
        var weekCode = WeekCodeFromSaturday(saturday);
        var prevWeek = PreviousWeekCode(weekCode);

        db.Users.AddRange(
            MakeUser(1, "admin@test.ma", level: 3, roleId: 1),
            MakeUser(10, "off@test.ma", level: 2, saturdayMode: (int)SaturdayWorkMode.AlternatingFullDay),
            MakeUser(11, "on@test.ma", level: 2, saturdayMode: (int)SaturdayWorkMode.AlternatingFullDay));

        // Semaine précédente : off a travaillé → intended ce samedi = OFF
        db.SaturdayHistories.Add(new SaturdayHistory
        {
            UserId = 10,
            SubServiceId = 1,
            WeekCode = prevWeek,
            WorkedSaturday = true
        });
        // on n'a pas travaillé → intended ON
        db.SaturdayHistories.Add(new SaturdayHistory
        {
            UserId = 11,
            SubServiceId = 1,
            WeekCode = prevWeek,
            WorkedSaturday = false
        });

        var shift = MakeShift(1, 8);
        db.SubServiceShiftConfigs.Add(shift);

        var planning = new WeeklyPlanning
        {
            Id = 1,
            SubServiceId = 1,
            WeekCode = weekCode,
            WeekStartDate = saturday.AddDays(-5),
            Status = PlanningStatus.Draft
        };
        db.WeeklyPlannings.Add(planning);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var created = await svc.CreateAsync(1, new CreatePlanningReinforcementRequestDto
        {
            SubServiceId = 1,
            SaturdayDate = saturday,
            SlotsNeeded = 1,
            Reason = "Sous-effectif samedi"
        });

        Assert.Contains(created.Volunteers, v => v.UserId == 10);
        Assert.DoesNotContain(created.Volunteers, v => v.UserId == 11);

        await svc.VolunteerAcceptAsync(created.Id, 10);
        var filled = await svc.SelectAsync(created.Id, 1, new SelectReinforcementVolunteersDto
        {
            UserIds = [10],
            ShiftConfigId = shift.Id
        });

        Assert.Equal("Filled", filled.Status);
        var assignment = await db.ShiftAssignments.SingleAsync(a =>
            a.UserId == 10 && a.IsSaturday && a.WeeklyPlanningId == planning.Id);
        Assert.True(assignment.IsReinforcement);
        Assert.True(assignment.IsManagerOverride);
        Assert.Equal(shift.Id, assignment.SubServiceShiftConfigId);

        // Aucune écriture d'historique pour la semaine du renfort
        Assert.False(await db.SaturdayHistories.AnyAsync(h =>
            h.UserId == 10 && h.WeekCode == weekCode));

        // Historique précédent intact → flip semaine suivante inchangé
        var prev = await db.SaturdayHistories.SingleAsync(h =>
            h.UserId == 10 && h.WeekCode == prevWeek);
        Assert.True(prev.WorkedSaturday);
    }

    [Fact]
    public async Task Create_excludes_every_half_day_always_on_and_intended_on()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);

        var saturday = NextSaturdayOnOrAfterToday();
        var weekCode = WeekCodeFromSaturday(saturday);
        var prevWeek = PreviousWeekCode(weekCode);

        db.Users.AddRange(
            MakeUser(1, "admin@test.ma", level: 3, roleId: 1),
            // always-on 4h (niveau 1 défaut ou mode explicite)
            MakeUser(20, "always@test.ma", level: 1, saturdayMode: (int)SaturdayWorkMode.EveryHalfDay),
            // alternance OFF (a travaillé la semaine précédente)
            MakeUser(21, "off@test.ma", level: 2, saturdayMode: (int)SaturdayWorkMode.AlternatingFullDay),
            // alternance ON
            MakeUser(22, "on@test.ma", level: 2, saturdayMode: (int)SaturdayWorkMode.AlternatingFullDay));

        db.SaturdayHistories.AddRange(
            new SaturdayHistory
            {
                UserId = 21, SubServiceId = 1, WeekCode = prevWeek, WorkedSaturday = true
            },
            new SaturdayHistory
            {
                UserId = 22, SubServiceId = 1, WeekCode = prevWeek, WorkedSaturday = false
            });
        db.SubServiceShiftConfigs.Add(MakeShift(1));
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var created = await svc.CreateAsync(1, new CreatePlanningReinforcementRequestDto
        {
            SubServiceId = 1,
            SaturdayDate = saturday,
            SlotsNeeded = 1,
            Reason = "Besoin renfort"
        });

        var ids = created.Volunteers.Select(v => v.UserId).ToHashSet();
        Assert.Contains(21, ids);
        Assert.DoesNotContain(20, ids);
        Assert.DoesNotContain(22, ids);
        Assert.Equal(1, created.EligibleCount);
    }

    [Fact]
    public async Task Select_rejects_when_more_volunteers_than_SlotsNeeded()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);

        var saturday = NextSaturdayOnOrAfterToday();
        var prevWeek = PreviousWeekCode(WeekCodeFromSaturday(saturday));

        db.Users.AddRange(
            MakeUser(1, "admin@test.ma", level: 3, roleId: 1),
            MakeUser(30, "a@test.ma", level: 2, saturdayMode: (int)SaturdayWorkMode.AlternatingFullDay),
            MakeUser(31, "b@test.ma", level: 2, saturdayMode: (int)SaturdayWorkMode.AlternatingFullDay));

        db.SaturdayHistories.AddRange(
            new SaturdayHistory
            {
                UserId = 30, SubServiceId = 1, WeekCode = prevWeek, WorkedSaturday = true
            },
            new SaturdayHistory
            {
                UserId = 31, SubServiceId = 1, WeekCode = prevWeek, WorkedSaturday = true
            });
        db.SubServiceShiftConfigs.Add(MakeShift(1));
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var created = await svc.CreateAsync(1, new CreatePlanningReinforcementRequestDto
        {
            SubServiceId = 1,
            SaturdayDate = saturday,
            SlotsNeeded = 1,
            Reason = "Un seul poste"
        });

        await svc.VolunteerAcceptAsync(created.Id, 30);
        await svc.VolunteerAcceptAsync(created.Id, 31);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.SelectAsync(created.Id, 1, new SelectReinforcementVolunteersDto
            {
                UserIds = [30, 31],
                ShiftConfigId = 1
            }));
        Assert.Contains("Maximum 1", ex.Message);
    }

    [Fact]
    public async Task Detail_scheduled_hours_week_and_month_exclude_leave_holiday_and_count_half_day()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);

        var saturday = NextSaturdayOnOrAfterToday();
        var monday = saturday.AddDays(-5);
        var weekCode = WeekCodeFromSaturday(saturday);
        var prevWeek = PreviousWeekCode(weekCode);
        var today = DateOnly.FromDateTime(DateTime.Today);

        db.Users.AddRange(
            MakeUser(1, "admin@test.ma", level: 3, roleId: 1),
            MakeUser(40, "vol@test.ma", level: 2, saturdayMode: (int)SaturdayWorkMode.AlternatingFullDay));

        db.SaturdayHistories.Add(new SaturdayHistory
        {
            UserId = 40, SubServiceId = 1, WeekCode = prevWeek, WorkedSaturday = true
        });

        var shift8 = MakeShift(1, 8, template: true);
        var shift4 = MakeShift(2, 4, template: true);
        db.SubServiceShiftConfigs.AddRange(shift8, shift4);

        var planning = new WeeklyPlanning
        {
            Id = 1,
            SubServiceId = 1,
            WeekCode = weekCode,
            WeekStartDate = monday,
            Status = PlanningStatus.Draft
        };
        db.WeeklyPlannings.Add(planning);

        // Semaine du samedi : Lun 8h, Mar congé (exclu), Mer 4h, Jeu férié (exclu), Ven 8h
        // = 8+4+8 = 20h semaine
        db.ShiftAssignments.AddRange(
            new ShiftAssignment
            {
                WeeklyPlanningId = 1,
                UserId = 40,
                AssignedDate = monday,
                DayOfWeek = DayOfWeekEnum.Monday,
                SubServiceShiftConfigId = 1
            },
            new ShiftAssignment
            {
                WeeklyPlanningId = 1,
                UserId = 40,
                AssignedDate = monday.AddDays(1),
                DayOfWeek = DayOfWeekEnum.Tuesday,
                SubServiceShiftConfigId = 1,
                IsOnLeave = true
            },
            new ShiftAssignment
            {
                WeeklyPlanningId = 1,
                UserId = 40,
                AssignedDate = monday.AddDays(2),
                DayOfWeek = DayOfWeekEnum.Wednesday,
                SubServiceShiftConfigId = 2
            },
            new ShiftAssignment
            {
                WeeklyPlanningId = 1,
                UserId = 40,
                AssignedDate = monday.AddDays(3),
                DayOfWeek = DayOfWeekEnum.Thursday,
                SubServiceShiftConfigId = 1,
                IsHoliday = true
            },
            new ShiftAssignment
            {
                WeeklyPlanningId = 1,
                UserId = 40,
                AssignedDate = monday.AddDays(4),
                DayOfWeek = DayOfWeekEnum.Friday,
                SubServiceShiftConfigId = 1
            });

        // Heure hors semaine du samedi mais dans le mois courant (si possible)
        var monthDay = new DateOnly(today.Year, today.Month, 1);
        if (monthDay < monday || monthDay > saturday)
        {
            // Planning factice pour une date du mois courant hors semaine cible
            var otherWeek = new WeeklyPlanning
            {
                Id = 2,
                SubServiceId = 1,
                WeekCode = "extra",
                WeekStartDate = monthDay,
                Status = PlanningStatus.Draft
            };
            db.WeeklyPlannings.Add(otherWeek);
            db.ShiftAssignments.Add(new ShiftAssignment
            {
                WeeklyPlanningId = 2,
                UserId = 40,
                AssignedDate = monthDay,
                DayOfWeek = DayOfWeekEnum.Monday,
                SubServiceShiftConfigId = 1
            });
        }

        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var created = await svc.CreateAsync(1, new CreatePlanningReinforcementRequestDto
        {
            SubServiceId = 1,
            SaturdayDate = saturday,
            SlotsNeeded = 1,
            Reason = "Heures à vérifier"
        });

        var vol = created.Volunteers.Single(v => v.UserId == 40);
        Assert.Equal(20m, vol.ScheduledHoursWeek);

        // Mois = mois calendaire courant : au minimum les heures de la semaine qui tombent dans le mois
        var weekHoursInMonth = 0m;
        foreach (var offset in new[] { 0, 2, 4 }) // lun, mer, ven comptés
        {
            var d = monday.AddDays(offset);
            if (d.Year == today.Year && d.Month == today.Month)
                weekHoursInMonth += offset == 2 ? 4m : 8m;
        }
        var extraMonth = (monthDay < monday || monthDay > saturday)
                         && monthDay.Year == today.Year && monthDay.Month == today.Month
            ? 8m
            : 0m;
        Assert.Equal(weekHoursInMonth + extraMonth, vol.ScheduledHoursMonth);
    }

    [Fact]
    public async Task Create_rejects_when_subService_outside_supervisor_perimeter()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Roles.Add(new Role { Id = 3, Name = "Superviseur" });
        db.SubServices.Add(new SubService { Id = 2, ServiceId = 1, Name = "Cellule B", Code = "CB" });
        db.Users.Add(new User
        {
            Id = 50,
            FirstName = "Sup",
            LastName = "Test",
            Email = "sup@test.ma",
            RoleId = 3,
            IsActive = true,
            SubServiceId = 1,
            Level = 3,
            PasswordHash = "x",
            HireDate = DateTime.UtcNow.AddYears(-1),
        });
        await db.SaveChangesAsync();
        db.UserSubServices.Add(new UserSubService { UserId = 50, SubServiceId = 1 });
        await db.SaveChangesAsync();

        var saturday = NextSaturdayOnOrAfterToday();
        var svc = CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync(50, new CreatePlanningReinforcementRequestDto
            {
                SubServiceId = 2,
                SaturdayDate = saturday,
                SlotsNeeded = 1,
                Reason = "Hors périmètre"
            }));
        Assert.Contains("périmètre", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetById_returns_null_when_outside_supervisor_perimeter()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Roles.Add(new Role { Id = 3, Name = "Superviseur" });
        db.SubServices.Add(new SubService { Id = 2, ServiceId = 1, Name = "Cellule B", Code = "CB" });
        db.Users.AddRange(
            MakeUser(1, "admin@test.ma", level: 3, roleId: 1),
            new User
            {
                Id = 51,
                FirstName = "Sup",
                LastName = "Other",
                Email = "sup2@test.ma",
                RoleId = 3,
                IsActive = true,
                SubServiceId = 2,
                Level = 3,
                PasswordHash = "x",
                HireDate = DateTime.UtcNow.AddYears(-1),
            });
        await db.SaveChangesAsync();
        db.UserSubServices.Add(new UserSubService { UserId = 51, SubServiceId = 2 });

        var saturday = NextSaturdayOnOrAfterToday();
        db.PlanningReinforcementRequests.Add(new PlanningReinforcementRequest
        {
            WeekCode = WeekCodeFromSaturday(saturday),
            SaturdayDate = saturday,
            SubServiceId = 1,
            SlotsNeeded = 1,
            Reason = "Cellule A",
            Status = PlanningReinforcementRequestStatus.Open,
            CreatedByUserId = 1,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var requestId = await db.PlanningReinforcementRequests.Select(r => r.Id).SingleAsync();
        var svc = CreateService(db);

        var asAdmin = await svc.GetByIdAsync(requestId, viewerUserId: 1);
        Assert.NotNull(asAdmin);

        var asOtherSup = await svc.GetByIdAsync(requestId, viewerUserId: 51);
        Assert.Null(asOtherSup);
    }

    [Fact]
    public async Task Create_allows_own_SubServiceId_when_managed_list_empty()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Roles.Add(new Role { Id = 3, Name = "Superviseur" });
        db.SubServices.Add(new SubService { Id = 2, ServiceId = 1, Name = "Cellule B", Code = "CB" });

        var saturday = NextSaturdayOnOrAfterToday();
        var prevWeek = PreviousWeekCode(WeekCodeFromSaturday(saturday));

        db.Users.AddRange(
            new User
            {
                Id = 52,
                FirstName = "Sup",
                LastName = "Fallback",
                Email = "supfb@test.ma",
                RoleId = 3,
                IsActive = true,
                SubServiceId = 1,
                Level = 3,
                PasswordHash = "x",
                HireDate = DateTime.UtcNow.AddYears(-1),
            },
            MakeUser(53, "off@test.ma", level: 2, saturdayMode: (int)SaturdayWorkMode.AlternatingFullDay));
        db.SaturdayHistories.Add(new SaturdayHistory
        {
            UserId = 53, SubServiceId = 1, WeekCode = prevWeek, WorkedSaturday = true
        });
        db.SubServiceShiftConfigs.Add(MakeShift(1));
        await db.SaveChangesAsync();
        // Pas de UserSubServices / ManagedServices → fallback SubServiceId

        var svc = CreateService(db);
        var created = await svc.CreateAsync(52, new CreatePlanningReinforcementRequestDto
        {
            SubServiceId = 1,
            SaturdayDate = saturday,
            SlotsNeeded = 1,
            Reason = "Fallback cellule propre"
        });
        Assert.Equal(1, created.SubServiceId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateAsync(52, new CreatePlanningReinforcementRequestDto
            {
                SubServiceId = 2,
                SaturdayDate = saturday,
                SlotsNeeded = 1,
                Reason = "Autre cellule"
            }));
        Assert.Contains("périmètre", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_uses_published_saturday_off_grid_when_assignments_exist()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);

        var saturday = NextSaturdayOnOrAfterToday();
        var weekCode = WeekCodeFromSaturday(saturday);
        var monday = saturday.AddDays(-5);

        db.Users.AddRange(
            MakeUser(1, "admin@test.ma", level: 3, roleId: 1),
            // Sans historique : ancien algo = intended ON → exclu ; grille OFF → éligible
            MakeUser(50, "off-grid@test.ma", level: 2, saturdayMode: (int)SaturdayWorkMode.AlternatingFullDay),
            MakeUser(51, "on-grid@test.ma", level: 2, saturdayMode: (int)SaturdayWorkMode.AlternatingFullDay));

        var shift = MakeShift(1, 8);
        db.SubServiceShiftConfigs.Add(shift);
        db.WeeklyPlannings.Add(new WeeklyPlanning
        {
            Id = 1,
            SubServiceId = 1,
            WeekCode = weekCode,
            WeekStartDate = monday,
            Status = PlanningStatus.Draft
        });
        db.ShiftAssignments.AddRange(
            new ShiftAssignment
            {
                WeeklyPlanningId = 1,
                UserId = 50,
                AssignedDate = saturday,
                DayOfWeek = DayOfWeekEnum.Saturday,
                IsSaturday = true,
                SubServiceShiftConfigId = null
            },
            new ShiftAssignment
            {
                WeeklyPlanningId = 1,
                UserId = 51,
                AssignedDate = saturday,
                DayOfWeek = DayOfWeekEnum.Saturday,
                IsSaturday = true,
                SubServiceShiftConfigId = 1
            });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var created = await svc.CreateAsync(1, new CreatePlanningReinforcementRequestDto
        {
            SubServiceId = 1,
            SaturdayDate = saturday,
            SlotsNeeded = 1,
            Reason = "OFF grille prioritaire"
        });

        var ids = created.Volunteers.Select(v => v.UserId).ToHashSet();
        Assert.Contains(50, ids);
        Assert.DoesNotContain(51, ids);
    }

    [Fact]
    public async Task GetContributorStats_ranks_by_selected_then_accepted()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);

        var saturday = NextSaturdayOnOrAfterToday();
        var weekCode = WeekCodeFromSaturday(saturday);

        db.Users.AddRange(
            MakeUser(1, "admin@test.ma", level: 3, roleId: 1),
            MakeUser(60, "top@test.ma", level: 2),
            MakeUser(61, "mid@test.ma", level: 2));

        db.PlanningReinforcementRequests.AddRange(
            new PlanningReinforcementRequest
            {
                Id = 1,
                WeekCode = weekCode,
                SaturdayDate = saturday,
                SubServiceId = 1,
                SlotsNeeded = 1,
                Reason = "A",
                Status = PlanningReinforcementRequestStatus.Filled,
                CreatedByUserId = 1,
                CreatedAt = DateTime.UtcNow
            },
            new PlanningReinforcementRequest
            {
                Id = 2,
                WeekCode = weekCode,
                SaturdayDate = saturday.AddDays(7),
                SubServiceId = 1,
                SlotsNeeded = 1,
                Reason = "B",
                Status = PlanningReinforcementRequestStatus.Open,
                CreatedByUserId = 1,
                CreatedAt = DateTime.UtcNow
            });

        db.PlanningReinforcementVolunteers.AddRange(
            new PlanningReinforcementVolunteer
            {
                RequestId = 1,
                UserId = 60,
                Status = PlanningReinforcementVolunteerStatus.Selected
            },
            new PlanningReinforcementVolunteer
            {
                RequestId = 1,
                UserId = 61,
                Status = PlanningReinforcementVolunteerStatus.Declined
            },
            new PlanningReinforcementVolunteer
            {
                RequestId = 2,
                UserId = 60,
                Status = PlanningReinforcementVolunteerStatus.Selected
            },
            new PlanningReinforcementVolunteer
            {
                RequestId = 2,
                UserId = 61,
                Status = PlanningReinforcementVolunteerStatus.Accepted
            });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var stats = await svc.GetContributorStatsAsync(viewerUserId: 1);

        Assert.Equal(2, stats.Count);
        Assert.Equal(60, stats[0].UserId);
        Assert.Equal(2, stats[0].Selected);
        Assert.Equal(2, stats[0].Accepted);
        Assert.Equal(2, stats[0].Solicited);

        Assert.Equal(61, stats[1].UserId);
        Assert.Equal(0, stats[1].Selected);
        Assert.Equal(1, stats[1].Accepted);
        Assert.Equal(1, stats[1].Declined);
        Assert.Equal(2, stats[1].Solicited);
        Assert.Equal(50m, stats[1].AcceptanceRate);
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
