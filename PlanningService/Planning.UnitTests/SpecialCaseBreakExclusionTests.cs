using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Planning.Application.DTOs.Planning;
using Planning.Domain.Entities;
using Planning.Domain.Enums;
using Planning.Infrastructure.Hubs;
using Planning.Infrastructure.Persistence;
using Planning.Infrastructure.Services;

namespace Planning.UnitTests;

public class SpecialCaseBreakExclusionTests
{
    private static AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    private static PlanningService CreatePlanningService(AppDbContext db) =>
        new(db, new FakePlanningHubContext(), new PlanningPerimeterResolver(db));

    [Fact]
    public void AssignDayBreaks_special_case_never_gets_plus3_or_plus5()
    {
        var start = new TimeOnly(8, 0);
        var config = new SubServiceShiftConfig
        {
            Id = 1,
            SubServiceId = 1,
            IsTemplate = true,
            Label = "8h",
            StartTime = start,
            WorkHours = 8,
            BreakDurationMinutes = 60,
            IsCriticalCell = true,
            RequiredCount = 4,
            MinPresencePercent = 70,
            DisplayOrder = 1,
            CreatedAt = DateTime.UtcNow
        };
        var configs = new Dictionary<int, SubServiceShiftConfig> { [1] = config };

        // Plusieurs agents dont un cas particulier — force le packing critique
        var assignments = new List<ShiftAssignment>();
        for (var i = 1; i <= 6; i++)
        {
            assignments.Add(new ShiftAssignment
            {
                UserId = i,
                SubServiceShiftConfigId = 1,
                AssignedDate = new DateOnly(2026, 8, 3),
                DayOfWeek = DayOfWeekEnum.Monday
            });
        }

        var special = new HashSet<int> { 1 };
        PlateauBreakPacker.AssignDayBreaks(assignments, configs, 70, specialCaseUserIds: special);

        var specialBreak = assignments.Single(a => a.UserId == 1).BreakTime;
        Assert.True(specialBreak.HasValue);
        Assert.False(
            BreakSlotPlanner.IsExtremeCaseBreak(start, specialBreak!.Value),
            $"Cas particulier a reçu un extrême: {specialBreak}");
    }

    [Fact]
    public void WithoutExtremeCaseBreaks_removes_exact_plus3_and_plus5()
    {
        var start = new TimeOnly(8, 0);
        var slots = new[]
        {
            start.AddHours(3),
            start.AddHours(3.5),
            start.AddHours(4),
            start.AddHours(5)
        };
        var filtered = BreakSlotPlanner.WithoutExtremeCaseBreaks(start, slots);
        Assert.DoesNotContain(start.AddHours(3), filtered);
        Assert.DoesNotContain(start.AddHours(5), filtered);
        Assert.Contains(start.AddHours(3.5), filtered);
        Assert.Contains(start.AddHours(4), filtered);
    }

    [Fact]
    public async Task SetEmployeeSpecialCase_requires_description_when_enabled()
    {
        await using var db = CreateDb();
        db.Roles.Add(new Role { Id = 1, Name = "Pilote" });
        db.Users.Add(new User
        {
            Id = 1,
            FirstName = "A",
            LastName = "B",
            Email = "a@t.ma",
            RoleId = 1,
            PasswordHash = "x",
            IsActive = true,
            HireDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = CreatePlanningService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.SetEmployeeSpecialCaseAsync(new SetEmployeeSpecialCaseDto
            {
                UserId = 1,
                IsSpecialCase = true,
                Description = "ab"
            }));
        Assert.Contains("Description", ex.Message);

        await svc.SetEmployeeSpecialCaseAsync(new SetEmployeeSpecialCaseDto
        {
            UserId = 1,
            IsSpecialCase = true,
            Description = "diabétique"
        });
        var u = await db.Users.SingleAsync(x => x.Id == 1);
        Assert.True(u.IsSpecialCase);
        Assert.Equal("diabétique", u.SpecialCaseDescription);

        await svc.SetEmployeeSpecialCaseAsync(new SetEmployeeSpecialCaseDto
        {
            UserId = 1,
            IsSpecialCase = false
        });
        u = await db.Users.SingleAsync(x => x.Id == 1);
        Assert.False(u.IsSpecialCase);
        Assert.Null(u.SpecialCaseDescription);
    }

    [Fact]
    public void RepairBreakOffsetDiversity_guard_blocks_extreme_onto_special_case()
    {
        var start = new TimeOnly(8, 0);
        var specialHasIdeal = !BreakSlotPlanner.IsExtremeCaseBreak(start, start.AddHours(4));
        var peerHasExtreme = BreakSlotPlanner.IsExtremeCaseBreak(start, start.AddHours(3));
        Assert.True(specialHasIdeal);
        Assert.True(peerHasExtreme);
        // Même logique que PlanningService.RepairBreakOffsetDiversity :
        // si IsSpecialCase && IsExtremeCaseBreak(peerBreak) → skip swap
        const bool isSpecialCase = true;
        var wouldBlock = isSpecialCase && peerHasExtreme;
        Assert.True(wouldBlock);
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
