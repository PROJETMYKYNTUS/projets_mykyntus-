using EmployeeDirectory.Domain.Entities;
using EmployeeDirectory.Infrastructure.Persistence;
using EmployeeDirectory.Infrastructure.Services;
using Kyntus.Messaging.Contracts;
using Kyntus.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using DomainAssignmentKind = EmployeeDirectory.Domain.Enums.OrgAssignmentKind;
using DomainNodeLevel = EmployeeDirectory.Domain.Enums.OrgNodeLevel;

namespace EmployeeDirectory.UnitTests;

public class OrgStructuralRoleExclusivityTests : IDisposable
{
    private readonly DirectoryDbContext _db;
    private readonly OrgStructuralRoleExclusivityService _service;

    public OrgStructuralRoleExclusivityTests()
    {
        var options = new DbContextOptionsBuilder<DirectoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new DirectoryDbContext(options);
        _service = new OrgStructuralRoleExclusivityService(_db, new NoopOutboxWriter());
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task RevokeConflicting_keeps_same_kind_assignments()
    {
        var employeeId = Guid.NewGuid();
        SeedEmployee(employeeId, KyntusRoleNames.ChefDeProjet, poleId: "pole-a");
        SeedAssignment(employeeId, DomainAssignmentKind.ChefDeProjet, "pole-a", DomainNodeLevel.Pole);
        SeedAssignment(employeeId, DomainAssignmentKind.ChefDeProjet, "pole-b", DomainNodeLevel.Pole);

        var revoked = await _service.RevokeConflictingStructuralRolesForEmployeeAsync(
            employeeId, DomainAssignmentKind.ChefDeProjet, null, "add pole");
        await _db.SaveChangesAsync();

        Assert.Empty(revoked);
        var active = await _db.OrgAssignments
            .Where(a => a.EmployeeId == employeeId && a.EffectiveTo == null)
            .Select(a => a.NodeId)
            .OrderBy(x => x)
            .ToListAsync();
        Assert.Equal(["pole-a", "pole-b"], active);
    }

    [Fact]
    public async Task RevokeConflicting_revokes_other_kinds()
    {
        var employeeId = Guid.NewGuid();
        SeedEmployee(employeeId, KyntusRoleNames.ChefDeProjet, poleId: "pole-a");
        SeedAssignment(employeeId, DomainAssignmentKind.ChefDeProjet, "pole-a", DomainNodeLevel.Pole);
        SeedAssignment(employeeId, DomainAssignmentKind.Superviseur, "cell-1", DomainNodeLevel.Cellule);

        var revoked = await _service.RevokeConflictingStructuralRolesForEmployeeAsync(
            employeeId, DomainAssignmentKind.Superviseur, null, "switch to superviseur");
        await _db.SaveChangesAsync();

        Assert.Contains(revoked, r => r.NodeId == "pole-a");
        var active = await _db.OrgAssignments
            .Where(a => a.EmployeeId == employeeId && a.EffectiveTo == null)
            .ToListAsync();
        Assert.Single(active);
        Assert.Equal(DomainAssignmentKind.Superviseur, active[0].Kind);
        Assert.Equal("cell-1", active[0].NodeId);
    }

    [Fact]
    public async Task RevokeConflicting_pilote_clears_all_including_other_pilote()
    {
        var employeeId = Guid.NewGuid();
        SeedEmployee(employeeId, KyntusRoleNames.Pilote, serviceId: "svc-a");
        SeedAssignment(employeeId, DomainAssignmentKind.Pilote, "svc-a", DomainNodeLevel.Service);
        SeedAssignment(employeeId, DomainAssignmentKind.ReferentTechnique, "svc-rt", DomainNodeLevel.Service);

        var revoked = await _service.RevokeConflictingStructuralRolesForEmployeeAsync(
            employeeId, DomainAssignmentKind.Pilote, null, "rotate");
        await _db.SaveChangesAsync();

        Assert.Equal(2, revoked.Count);
        Assert.Empty(await _db.OrgAssignments
            .Where(a => a.EmployeeId == employeeId && a.EffectiveTo == null)
            .ToListAsync());
    }

    private void SeedEmployee(Guid id, string role, string? poleId = null, string? celluleId = null, string? serviceId = null)
    {
        _db.Employees.Add(new Employee
        {
            Id = id,
            Email = $"{id:N}@test.local",
            FirstName = "Test",
            LastName = "User",
            Role = role,
            PoleId = poleId,
            CelluleId = celluleId,
            ServiceId = serviceId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        _db.SaveChanges();
    }

    private void SeedAssignment(Guid employeeId, DomainAssignmentKind kind, string nodeId, DomainNodeLevel level)
    {
        _db.OrgAssignments.Add(new OrgAssignment
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            NodeId = nodeId,
            NodeLevel = level,
            EmployeeId = employeeId,
            EffectiveFrom = DateTime.UtcNow.AddDays(-1),
        });
        _db.SaveChanges();
    }

    private sealed class NoopOutboxWriter : IOutboxWriter
    {
        public Task EnqueueAsync<T>(T message, string? aggregateId = null, string? correlationId = null, CancellationToken ct = default)
            where T : class => Task.CompletedTask;
    }
}
