using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Planning.Application.DTOs.Planning;
using Planning.Domain.Entities;
using Planning.Domain.Enums;
using Planning.Infrastructure.Hubs;
using Planning.Infrastructure.Persistence;
using Planning.Infrastructure.Services;

namespace Planning.UnitTests;

/// <summary>
/// Regen partielle : les jours &lt; regenerateFrom restent inchangés en base ;
/// toute la semaine (Lun–Sam) reste complète.
/// </summary>
public class PartialRegenPreservesPastTests
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

    [Fact]
    public async Task Partial_regen_from_wednesday_keeps_mon_tue_and_fills_full_week()
    {
        await using var db = CreateDb();
        await SeedBaseAsync(db);
        db.Users.AddRange(
            MakeUser(1, "a@test.ma", level: 2),
            MakeUser(2, "b@test.ma", level: 2));
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

        var monday = new DateOnly(2026, 7, 20); // ISO week 30
        var tuesday = monday.AddDays(1);
        var wednesday = monday.AddDays(2);
        var saturday = monday.AddDays(5);

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

        var before = await db.ShiftAssignments
            .AsNoTracking()
            .Where(a => a.WeeklyPlanningId == planning.Id)
            .ToListAsync();

        var monBefore = before
            .Where(a => a.AssignedDate == monday)
            .OrderBy(a => a.UserId)
            .Select(a => (a.UserId, a.SubServiceShiftConfigId, a.IsOnLeave, a.IsHoliday))
            .ToList();
        var tueBefore = before
            .Where(a => a.AssignedDate == tuesday)
            .OrderBy(a => a.UserId)
            .Select(a => (a.UserId, a.SubServiceShiftConfigId, a.IsOnLeave, a.IsHoliday))
            .ToList();

        Assert.Equal(2, monBefore.Count);
        Assert.Equal(2, tueBefore.Count);

        // Marquer Lun/Mar pour détecter toute réécriture accidentelle
        foreach (var a in await db.ShiftAssignments
                     .Where(x => x.WeeklyPlanningId == planning.Id
                                 && x.AssignedDate < wednesday)
                     .ToListAsync())
        {
            a.IsManagerOverride = true;
        }
        await db.SaveChangesAsync();

        await svc.GeneratePlanningFromConfigAsync(new GeneratePlanningFromConfigDto
        {
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeeklyPlanningId = planning.Id,
            RegenerateFromDate = wednesday
        });

        var after = await db.ShiftAssignments
            .AsNoTracking()
            .Where(a => a.WeeklyPlanningId == planning.Id)
            .ToListAsync();

        // Semaine Lun–Ven complète pour chaque user (samedi OFF peut n'avoir aucune ligne)
        foreach (var userId in new[] { 1, 2 })
        {
            var dates = after.Where(a => a.UserId == userId).Select(a => a.AssignedDate).ToHashSet();
            for (var d = monday; d <= monday.AddDays(4); d = d.AddDays(1))
                Assert.Contains(d, dates);
        }

        // Au moins une affectation samedi régénérée (ON)
        Assert.Contains(after, a => a.AssignedDate == saturday);

        // Lun–Mar inchangés (flag + créneaux)
        var monAfter = after
            .Where(a => a.AssignedDate == monday)
            .OrderBy(a => a.UserId)
            .ToList();
        var tueAfter = after
            .Where(a => a.AssignedDate == tuesday)
            .OrderBy(a => a.UserId)
            .ToList();

        Assert.Equal(2, monAfter.Count);
        Assert.Equal(2, tueAfter.Count);
        Assert.All(monAfter, a => Assert.True(a.IsManagerOverride));
        Assert.All(tueAfter, a => Assert.True(a.IsManagerOverride));

        for (var i = 0; i < 2; i++)
        {
            Assert.Equal(monBefore[i].UserId, monAfter[i].UserId);
            Assert.Equal(monBefore[i].SubServiceShiftConfigId, monAfter[i].SubServiceShiftConfigId);
            Assert.Equal(tueBefore[i].UserId, tueAfter[i].UserId);
            Assert.Equal(tueBefore[i].SubServiceShiftConfigId, tueAfter[i].SubServiceShiftConfigId);
        }

        // Mer–Ven régénérés (pas le flag override posé uniquement sur le passé)
        Assert.All(
            after.Where(a => a.AssignedDate >= wednesday && a.AssignedDate <= monday.AddDays(4)),
            a => Assert.False(a.IsManagerOverride));
        Assert.True(after.Count(a => a.AssignedDate >= wednesday && a.AssignedDate <= monday.AddDays(4)) >= 2 * 3);
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
