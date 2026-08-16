using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Planning.Domain.Entities;
using Planning.Domain.Enums;
using Planning.Infrastructure.Hubs;
using Planning.Infrastructure.Persistence;
using Planning.Infrastructure.Services;

namespace Planning.UnitTests;

public class PendingRequestsAlertTests
{
    private static AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    private static PlanningPendingRequestsAlertService CreateService(AppDbContext db) =>
        new(db, new FakePlanningHubContext(), NullLogger<PlanningPendingRequestsAlertService>.Instance,
            new PlanningPerimeterResolver(db));

    private static async Task SeedBaseAsync(AppDbContext db)
    {
        db.Roles.AddRange(
            new Role { Id = 1, Name = "Admin" },
            new Role { Id = 2, Name = "RH" },
            new Role { Id = 3, Name = "Superviseur" },
            new Role { Id = 4, Name = "Pilote" });
        db.Floors.Add(new Floor { Id = 1, Name = "F1", FloorNumber = 1 });
        db.Services.Add(new Service { Id = 1, FloorId = 1, Name = "Svc", Code = "S1" });
        db.SubServices.AddRange(
            new SubService { Id = 1, ServiceId = 1, Name = "Cell A", Code = "CA" },
            new SubService { Id = 2, ServiceId = 1, Name = "Cell B", Code = "CB" });
        db.PlanningAutoGenerateSettings.Add(new PlanningAutoGenerateSettings());
        await db.SaveChangesAsync();
    }

    private static User MakeUser(int id, int roleId, string email, int? authId = null) => new()
    {
        Id = id,
        FirstName = $"U{id}",
        LastName = "T",
        Email = email,
        RoleId = roleId,
        IsActive = true,
        SubServiceId = 1,
        Level = 2,
        PasswordHash = "x",
        HireDate = DateTime.UtcNow.AddYears(-1),
        AuthUserId = authId
    };

    private static async Task<(WeeklyPlanning planning, ShiftAssignment assignment)> SeedPlanningAssignmentAsync(
        AppDbContext db, int userId, int subServiceId, string weekCode = "2026-W32")
    {
        var planning = new WeeklyPlanning
        {
            SubServiceId = subServiceId,
            WeekCode = weekCode,
            WeekStartDate = new DateOnly(2026, 8, 3),
            Status = PlanningStatus.Draft
        };
        db.WeeklyPlannings.Add(planning);
        await db.SaveChangesAsync();

        var assignment = new ShiftAssignment
        {
            WeeklyPlanningId = planning.Id,
            UserId = userId,
            AssignedDate = new DateOnly(2026, 8, 4),
            DayOfWeek = DayOfWeekEnum.Tuesday
        };
        db.ShiftAssignments.Add(assignment);
        await db.SaveChangesAsync();
        return (planning, assignment);
    }

    [Fact]
    public async Task GetSummary_counts_only_pending_statuses()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);

        db.Users.Add(MakeUser(10, 4, "pilote@t.ma"));
        await db.SaveChangesAsync();
        var (_, a1) = await SeedPlanningAssignmentAsync(db, 10, 1);
        var (_, a2) = await SeedPlanningAssignmentAsync(db, 10, 1, "2026-W33");

        db.PlanningChangeRequests.AddRange(
            new PlanningChangeRequest
            {
                WeekCode = "2026-W32",
                RequesterUserId = 10,
                CurrentAssignmentId = a1.Id,
                Reason = "switch pending partner",
                Status = PlanningChangeRequestStatus.PendingPartner
            },
            new PlanningChangeRequest
            {
                WeekCode = "2026-W33",
                RequesterUserId = 10,
                CurrentAssignmentId = a2.Id,
                Reason = "approved ignored",
                Status = PlanningChangeRequestStatus.Approved
            });

        db.PlanningExceptionalRequests.AddRange(
            new PlanningExceptionalRequest
            {
                WeekCode = "2026-W32",
                RequestedDate = new DateOnly(2026, 8, 5),
                RequesterUserId = 10,
                SubServiceId = 1,
                RequestedShiftTemplateId = 0,
                Reason = "exc pending rh",
                Status = PlanningExceptionalRequestStatus.PendingRh
            },
            new PlanningExceptionalRequest
            {
                WeekCode = "2026-W32",
                RequestedDate = new DateOnly(2026, 8, 6),
                RequesterUserId = 10,
                SubServiceId = 1,
                RequestedShiftTemplateId = 0,
                Reason = "rejected ignored",
                Status = PlanningExceptionalRequestStatus.Rejected
            });
        await db.SaveChangesAsync();

        var summary = await CreateService(db).GetSummaryAsync();
        Assert.Equal(1, summary.ChangePendingCount);
        Assert.Equal(1, summary.ExceptionalPendingCount);
        Assert.Equal(2, summary.TotalPendingCount);
        Assert.Equal(1, summary.ChangePendingPartner);
        Assert.Equal(1, summary.ExceptionalPendingRh);
    }

    [Fact]
    public async Task GetSummary_scopes_supervisor_to_managed_cells()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);

        db.Users.AddRange(
            MakeUser(10, 4, "a@t.ma"),
            MakeUser(11, 4, "b@t.ma"),
            MakeUser(20, 3, "sup@t.ma", authId: 200));
        await db.SaveChangesAsync();

        db.UserSubServices.Add(new UserSubService { UserId = 20, SubServiceId = 1 });
        await db.SaveChangesAsync();

        var (_, aCell1) = await SeedPlanningAssignmentAsync(db, 10, 1);
        var (_, aCell2) = await SeedPlanningAssignmentAsync(db, 11, 2, "2026-W33");

        // Reload assignment weekly planning sub ids — Seed uses subServiceId on planning
        db.PlanningChangeRequests.Add(new PlanningChangeRequest
        {
            WeekCode = "2026-W32",
            RequesterUserId = 10,
            CurrentAssignmentId = aCell1.Id,
            Reason = "in scope",
            Status = PlanningChangeRequestStatus.PendingSupervisor
        });
        db.PlanningExceptionalRequests.Add(new PlanningExceptionalRequest
        {
            WeekCode = "2026-W33",
            RequestedDate = new DateOnly(2026, 8, 12),
            RequesterUserId = 11,
            SubServiceId = 2,
            RequestedShiftTemplateId = 0,
            Reason = "out of scope",
            Status = PlanningExceptionalRequestStatus.PendingSupervisor
        });
        await db.SaveChangesAsync();

        // Attach managed nav for GetManagedSubServiceIdsAsync
        var sup = await db.Users
            .Include(u => u.ManagedSubServices)
            .Include(u => u.ManagedServices)
            .Include(u => u.Role)
            .FirstAsync(u => u.Id == 20);

        var summary = await CreateService(db).GetSummaryAsync(viewerUserId: 20);
        Assert.Equal(1, summary.ChangePendingCount);
        Assert.Equal(0, summary.ExceptionalPendingCount);
        Assert.Equal(1, summary.TotalPendingCount);
    }

    [Fact]
    public async Task SendJ1Reminders_sends_once_then_idempotent()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);

        db.Users.AddRange(
            MakeUser(1, 2, "rh@t.ma", authId: 101),
            MakeUser(10, 4, "pilote@t.ma"),
            MakeUser(20, 3, "sup@t.ma", authId: 200));
        await db.SaveChangesAsync();
        db.UserSubServices.Add(new UserSubService { UserId = 20, SubServiceId = 1 });
        await db.SaveChangesAsync();

        var (_, a) = await SeedPlanningAssignmentAsync(db, 10, 1);
        db.PlanningChangeRequests.Add(new PlanningChangeRequest
        {
            WeekCode = "2026-W32",
            RequesterUserId = 10,
            CurrentAssignmentId = a.Id,
            Reason = "pending",
            Status = PlanningChangeRequestStatus.PendingPartner
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var day = new DateOnly(2026, 8, 5);
        Assert.True(await svc.SendJ1RemindersAsync(day));
        Assert.False(await svc.SendJ1RemindersAsync(day));

        var notifs = await db.PlanningNotifications.ToListAsync();
        Assert.True(notifs.Count >= 1);
        Assert.Contains(notifs, n => n.SubServiceName == "Alertes demandes");
    }

    [Fact]
    public async Task SendJ1Reminders_silent_when_no_pending_but_marks_date()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Users.Add(MakeUser(1, 2, "rh@t.ma", authId: 101));
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var day = new DateOnly(2026, 8, 5);
        Assert.False(await svc.SendJ1RemindersAsync(day));
        var settings = await db.PlanningAutoGenerateSettings.SingleAsync();
        Assert.Equal(day, settings.LastPendingJ1ReminderDate);
        Assert.Empty(await db.PlanningNotifications.ToListAsync());
    }

    [Fact]
    public async Task SendValidationReminders_once_per_weekCode()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);

        db.Users.AddRange(
            MakeUser(1, 2, "rh@t.ma", authId: 101),
            MakeUser(10, 4, "pilote@t.ma"));
        await db.SaveChangesAsync();

        db.PlanningExceptionalRequests.Add(new PlanningExceptionalRequest
        {
            WeekCode = "2026-W32",
            RequestedDate = new DateOnly(2026, 8, 5),
            RequesterUserId = 10,
            SubServiceId = 1,
            RequestedShiftTemplateId = 0,
            Reason = "still pending",
            Status = PlanningExceptionalRequestStatus.PendingSupervisor
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        Assert.True(await svc.SendValidationRemindersAsync("2026-W40"));
        Assert.False(await svc.SendValidationRemindersAsync("2026-W40"));

        var notifs = await db.PlanningNotifications.ToListAsync();
        Assert.Single(notifs);
        Assert.Equal("Validation plannings", notifs[0].SubServiceName);
        Assert.Contains("exceptionnel", notifs[0].Message);
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
