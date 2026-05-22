using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Services;
using Xunit;

namespace PrimeBackend.Tests;

public sealed class PrimeFicheValidationSubmissionTests
{
    private static (PrimeDbContext Db, SqliteConnection Connection) CreateDb()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var opts = new DbContextOptionsBuilder<PrimeDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new PrimeDbContext(opts);
        db.Database.EnsureCreated();
        return (db, connection);
    }

    [Fact]
    public async Task ApplyResolvedDraftToFiche_UpdatesDraftLinkAndSupervisor()
    {
        var fiche = new EmployeePrimeServiceFicheEntity
        {
            Id = Guid.NewGuid(),
            CellulePrimeDraftId = Guid.Empty,
            SupervisorUserId = "old-sup",
            EmployeeId = "e2",
            ServiceId = "c1",
            CelluleId = "p1",
            Period = "2026-02",
            FillingStatus = "Complete",
            ValidationStatus = PrimeValidationWorkflowService.AwaitingData,
            ServiceSaisieJson = "{}",
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        var draft = new SupervisorCellulePrimeDraftEntity
        {
            Id = Guid.NewGuid(),
            SupervisorUserId = "e9",
            RootPoleId = "d1",
            CelluleId = "p1",
            Period = "2026-02",
            Status = "Validated",
            TemplateId = "t1",
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        PrimeFicheValidationSubmissionService.ApplyResolvedDraftToFiche(fiche, draft);

        Assert.Equal(draft.Id, fiche.CellulePrimeDraftId);
        Assert.Equal("e9", fiche.SupervisorUserId);
        Assert.True(PrimeFicheValidationSubmissionService.ComputeIsReadyForValidation(draft, fiche));
    }

    [Fact]
    public async Task IsPilotInReferentValidationScope_SameServiceId_ReturnsTrue()
    {
        var (db, connection) = CreateDb();
        await using (connection)
        await using (db)
        {
            var rtId = "e8";
            var pilotId = "e2";
            const string serviceId = "c1";
            const string celluleId = "p1";

            db.Employees.AddRange(
                new EmployeeEntity
                {
                    Id = rtId, FirstName = "RT", LastName = "X", Role = "Référent technique",
                    ParentId = "e9", PoleId = "d1", CelluleId = celluleId, ServiceId = serviceId, Email = "rt@test",
                },
                new EmployeeEntity
                {
                    Id = pilotId, FirstName = "P", LastName = "Y", Role = "Pilote",
                    ParentId = "other-rt", PoleId = "d1", CelluleId = celluleId, ServiceId = serviceId,
                    Email = "p@test",
                });
            await db.SaveChangesAsync();

            var org = new PrimeOrgScopeService(db);
            var inScope = await org.IsPilotInReferentValidationScopeAsync(rtId, pilotId);
            Assert.True(inScope);
        }
    }

    [Fact]
    public async Task IsPilotInReferentValidationScope_DirectParent_ReturnsTrue()
    {
        var (db, connection) = CreateDb();
        await using (connection)
        await using (db)
        {
            var rtId = "e8";
            var pilotId = "e2";
            db.Employees.AddRange(
                new EmployeeEntity
                {
                    Id = rtId, FirstName = "RT", LastName = "X", Role = "Référent technique",
                    ParentId = "e9", PoleId = "d1", CelluleId = "p1", ServiceId = "c1", Email = "rt@test",
                },
                new EmployeeEntity
                {
                    Id = pilotId, FirstName = "Mehdi", LastName = "C", Role = "Pilote",
                    ParentId = rtId, PoleId = "d1", CelluleId = "p1", ServiceId = "c2", Email = "m@test",
                });
            await db.SaveChangesAsync();

            var org = new PrimeOrgScopeService(db);
            Assert.True(await org.IsPilotInReferentValidationScopeAsync(rtId, pilotId));
        }
    }

    [Fact]
    public async Task CanAccessFiche_ReferentTechnique_ReadsPendingFiche_OnSharedService()
    {
        var (db, connection) = CreateDb();
        await using (connection)
        await using (db)
        {
            var rtId = "e8";
            var pilotId = "e2";
            const string serviceId = "c1";

            db.Employees.AddRange(
                new EmployeeEntity
                {
                    Id = rtId, FirstName = "RT", LastName = "X", Role = "Référent technique",
                    ParentId = "e9", PoleId = "d1", CelluleId = "p1", ServiceId = serviceId, Email = "rt@test",
                },
                new EmployeeEntity
                {
                    Id = pilotId, FirstName = "P", LastName = "Y", Role = "Pilote",
                    ParentId = "other", PoleId = "d1", CelluleId = "p1", ServiceId = serviceId, Email = "p@test",
                });
            db.RbacPermissions.Add(new RbacPermissionEntity
            {
                Id = Guid.NewGuid(),
                Role = "Référent technique",
                Action = "Read",
                Scope = "Service",
                IsAllowed = true,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();

            var fiche = new EmployeePrimeServiceFicheEntity
            {
                Id = Guid.NewGuid(),
                CellulePrimeDraftId = Guid.NewGuid(),
                SupervisorUserId = "e9",
                EmployeeId = pilotId,
                ServiceId = serviceId,
                CelluleId = "p1",
                Period = "2026-02",
                FillingStatus = "Complete",
                ValidationStatus = PrimeValidationWorkflowService.Pending,
                ServiceSaisieJson = "{}",
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            var org = new PrimeOrgScopeService(db);
            var rbac = new PrimeRbacReadService(db, org);
            var referent = await db.Employees.SingleAsync(e => e.Id == rtId);
            Assert.True(await rbac.CanAccessFicheAsync(referent, fiche, "Read"));
        }
    }

    [Fact]
    public async Task SyncForDraftAsync_AllCompletePilotsInCellule_BecomePending()
    {
        var (db, connection) = CreateDb();
        await using (connection)
        await using (db)
        {
            const string period = "2026-03";
            var draftId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            db.SupervisorCellulePrimeDrafts.Add(new SupervisorCellulePrimeDraftEntity
            {
                Id = draftId,
                SupervisorUserId = "e9",
                RootPoleId = "d1",
                CelluleId = "p1",
                Period = period,
                Status = "Validated",
                TemplateId = "t1",
                UpdatedAt = now,
            });
            db.Employees.AddRange(
                new EmployeeEntity
                {
                    Id = "pil-a", FirstName = "A", LastName = "One", Role = "Pilote",
                    ParentId = "e8", PoleId = "d1", CelluleId = "p1", ServiceId = "c1", Email = "a@test",
                },
                new EmployeeEntity
                {
                    Id = "pil-b", FirstName = "B", LastName = "Two", Role = "Pilote",
                    ParentId = "e8", PoleId = "d1", CelluleId = "p1", ServiceId = "c1", Email = "b@test",
                });
            db.Cellules.Add(new CelluleEntity { Id = "p1", Name = "Cell", PoleId = "d1" });
            db.EmployeePrimeServiceFiches.AddRange(
                new EmployeePrimeServiceFicheEntity
                {
                    Id = Guid.NewGuid(),
                    CellulePrimeDraftId = Guid.Empty,
                    SupervisorUserId = "e9",
                    EmployeeId = "pil-a",
                    ServiceId = "c1",
                    CelluleId = "p1",
                    Period = period,
                    FillingStatus = "Complete",
                    ValidationStatus = PrimeValidationWorkflowService.AwaitingData,
                    ServiceSaisieJson = "{}",
                    UpdatedAt = now,
                },
                new EmployeePrimeServiceFicheEntity
                {
                    Id = Guid.NewGuid(),
                    CellulePrimeDraftId = Guid.Empty,
                    SupervisorUserId = "e9",
                    EmployeeId = "pil-b",
                    ServiceId = "c1",
                    CelluleId = "p1",
                    Period = period,
                    FillingStatus = "Complete",
                    ValidationStatus = PrimeValidationWorkflowService.AwaitingData,
                    ServiceSaisieJson = "{}",
                    UpdatedAt = now,
                });
            await db.SaveChangesAsync();

            var org = new PrimeOrgScopeService(db);
            var wf = new PrimeValidationWorkflowRuntime(db);
            var submission = new PrimeFicheValidationSubmissionService(db, wf, org);
            await submission.SyncForDraftAsync(draftId);
            await db.SaveChangesAsync();

            var statuses = await db.EmployeePrimeServiceFiches
                .Where(f => f.Period == period)
                .Select(f => f.ValidationStatus)
                .ToListAsync();
            Assert.All(statuses, s => Assert.Equal(PrimeValidationWorkflowService.Pending, s));
        }
    }
}
