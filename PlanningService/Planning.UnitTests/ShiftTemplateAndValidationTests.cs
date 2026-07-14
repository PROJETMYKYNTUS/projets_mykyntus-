using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Planning.Application.DTOs.Planning;
using Planning.Domain.Entities;
using Planning.Domain.Enums;
using Planning.Infrastructure.Hubs;
using Planning.Infrastructure.Persistence;
using Planning.Infrastructure.Services;

namespace Planning.UnitTests;

public class ShiftTemplateAndValidationTests
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

    private static async Task SeedSubServiceAsync(AppDbContext db)
    {
        db.Roles.Add(new Role { Id = 1, Name = "Employee" });
        db.Floors.Add(new Floor { Id = 1, Name = "Floor 1", FloorNumber = 1 });
        db.Services.Add(new Service { Id = 1, FloorId = 1, Name = "Svc", Code = "S1" });
        db.SubServices.Add(new SubService { Id = 1, ServiceId = 1, Name = "Cellule A", Code = "CA" });
        db.Users.Add(new User
        {
            Id = 10,
            FirstName = "Admin",
            LastName = "Test",
            Email = "admin@test.ma",
            RoleId = 1,
            IsActive = true,
            SubServiceId = 1
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SaveShiftTemplate_replaces_existing_template_rows()
    {
        await using var db = CreateDb();
        await SeedSubServiceAsync(db);
        var svc = CreateService(db);

        await svc.SaveShiftTemplateAsync(new SaveShiftConfigDto
        {
            SubServiceId = 1,
            Shifts =
            [
                new ShiftConfigItemDto { Label = "Matin", StartTime = "08:00", WorkHours = 8, RequiredCount = 2, DisplayOrder = 1 },
                new ShiftConfigItemDto { Label = "Tardif", StartTime = "10:00", WorkHours = 8, RequiredCount = 2, DisplayOrder = 2 },
            ]
        });

        await svc.SaveShiftTemplateAsync(new SaveShiftConfigDto
        {
            SubServiceId = 1,
            Shifts =
            [
                new ShiftConfigItemDto { Label = "Unique", StartTime = "09:00", WorkHours = 8, RequiredCount = 4, DisplayOrder = 1 },
            ]
        });

        var templates = await db.SubServiceShiftConfigs.Where(c => c.IsTemplate).ToListAsync();
        Assert.Single(templates);
        Assert.Equal("Unique", templates[0].Label);
        Assert.Null(templates[0].WeekCode);
    }

    [Fact]
    public async Task EnsureWeekSnapshot_clones_template_and_is_idempotent()
    {
        await using var db = CreateDb();
        await SeedSubServiceAsync(db);
        var svc = CreateService(db);

        await svc.SaveShiftTemplateAsync(new SaveShiftConfigDto
        {
            SubServiceId = 1,
            Shifts =
            [
                new ShiftConfigItemDto { Label = "Matin", StartTime = "08:00", WorkHours = 8, RequiredCount = 3, DisplayOrder = 1 },
            ]
        });

        var monday = new DateOnly(2026, 7, 20);
        await svc.EnsureWeekSnapshotAsync(1, "2026-W30", monday);
        await svc.EnsureWeekSnapshotAsync(1, "2026-W30", monday);

        var snaps = await db.SubServiceShiftConfigs
            .Where(c => !c.IsTemplate && c.WeekCode == "2026-W30")
            .ToListAsync();
        Assert.Single(snaps);
        Assert.Equal("Matin", snaps[0].Label);
        Assert.Equal(monday, snaps[0].WeekStartDate);
    }

    [Fact]
    public async Task Publish_requires_consultation()
    {
        await using var db = CreateDb();
        await SeedSubServiceAsync(db);
        var svc = CreateService(db);

        db.WeeklyPlannings.Add(new WeeklyPlanning
        {
            Id = 1,
            SubServiceId = 1,
            WeekCode = "2026-W30",
            WeekStartDate = new DateOnly(2026, 7, 20),
            TotalEffectif = 1,
            Status = PlanningStatus.Draft,
            SaturdayGroupId = 1,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.PublishPlanningAsync(1, 10));
        Assert.Contains("Consultation", ex.Message);

        await svc.RecordConsultationAsync(1, 10);
        var published = await svc.PublishPlanningAsync(1, 10);
        Assert.Equal("Published", published.Status);
    }

    [Fact]
    public async Task GetShiftConfigStatus_reports_template_presence()
    {
        await using var db = CreateDb();
        await SeedSubServiceAsync(db);
        db.SubServices.Add(new SubService { Id = 2, ServiceId = 1, Name = "Cellule B", Code = "CB", PrimeServiceId = "prime-b" });
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        await svc.SaveShiftTemplateAsync(new SaveShiftConfigDto
        {
            SubServiceId = 1,
            Shifts =
            [
                new ShiftConfigItemDto { Label = "Matin", StartTime = "08:00", WorkHours = 8, RequiredCount = 2, DisplayOrder = 1 },
            ]
        });

        var status = await svc.GetShiftConfigStatusAsync();
        Assert.Equal(2, status.TotalCount);
        Assert.Equal(1, status.ConfiguredCount);
        Assert.Contains(status.Items, i => i.SubServiceId == 1 && i.HasTemplate && i.ShiftCount == 1);
        Assert.Contains(status.Items, i => i.SubServiceId == 2 && !i.HasTemplate);
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
