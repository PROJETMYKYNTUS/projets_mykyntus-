using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Services;
using Xunit;

namespace PrimeBackend.Tests;

public sealed class PrimeGlobalSynthesisReadinessTests
{
    private static (PrimeDbContext Db, SqliteConnection Conn, PrimeGlobalSynthesisReadinessService Readiness) Create()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var opts = new DbContextOptionsBuilder<PrimeDbContext>().UseSqlite(conn).Options;
        var db = new PrimeDbContext(opts);
        db.Database.EnsureCreated();
        var wf = new PrimeValidationWorkflowRuntime(db);
        var readiness = new PrimeGlobalSynthesisReadinessService(db, wf);
        return (db, conn, readiness);
    }

    [Fact]
    public async Task Service_not_ready_when_fiche_pending()
    {
        var (db, conn, readiness) = Create();
        using (conn)
        {
            db.Poles.Add(new PoleEntity { Id = "p1", Name = "Pôle" });
            db.Cellules.Add(new CelluleEntity { Id = "c1", Name = "Cell", PoleId = "p1" });
            db.Services.Add(new ServiceEntity { Id = "s1", Name = "Svc", CelluleId = "c1" });
            db.Employees.Add(new EmployeeEntity
            {
                Id = "e1",
                FirstName = "A",
                LastName = "B",
                Role = "Pilote",
                ServiceId = "s1",
                CelluleId = "c1",
                PoleId = "p1",
                Email = "a@b.c",
            });
            var draftId = Guid.NewGuid();
            db.SupervisorCellulePrimeDrafts.Add(new SupervisorCellulePrimeDraftEntity
            {
                Id = draftId,
                SupervisorUserId = "sup",
                RootPoleId = "p1",
                CelluleId = "c1",
                Period = "2026-05",
                TemplateId = "t",
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            db.EmployeePrimeServiceFiches.Add(new EmployeePrimeServiceFicheEntity
            {
                Id = Guid.NewGuid(),
                CellulePrimeDraftId = draftId,
                EmployeeId = "e1",
                ServiceId = "s1",
                CelluleId = "c1",
                Period = "2026-05",
                FillingStatus = "Complete",
                ValidationStatus = "Pending",
                SupervisorUserId = "sup",
                ServiceSaisieJson = "{}",
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();

            db.WorkflowSteps.Add(new WorkflowStepConfigEntity
            {
                Id = Guid.NewGuid(),
                ApproverRole = "Superviseur",
                FromStatus = "Pending",
                ToStatus = "Superviseur Approved",
                SortOrder = 1,
                IsActive = true,
                TerminalApproved = false,
            });
            await db.SaveChangesAsync();

            var dto = await readiness.GetReadinessAsync("2026-05");
            var svc = dto.Services.Single(s => s.ServiceId == "s1");
            Assert.False(svc.Ready);
            Assert.False(await readiness.IsScopeReadyAsync("2026-05", GlobalPoolScopeTypes.Service, "s1"));
        }
    }

    [Fact]
    public async Task Service_ready_when_terminal_and_complete()
    {
        var (db, conn, readiness) = Create();
        using (conn)
        {
            db.Poles.Add(new PoleEntity { Id = "p1", Name = "Pôle" });
            db.Cellules.Add(new CelluleEntity { Id = "c1", Name = "Cell", PoleId = "p1" });
            db.Services.Add(new ServiceEntity { Id = "s1", Name = "Svc", CelluleId = "c1" });
            db.Employees.Add(new EmployeeEntity
            {
                Id = "e1",
                FirstName = "A",
                LastName = "B",
                Role = "Pilote",
                ServiceId = "s1",
                CelluleId = "c1",
                PoleId = "p1",
                Email = "a@b.c",
            });
            var draftId = Guid.NewGuid();
            db.SupervisorCellulePrimeDrafts.Add(new SupervisorCellulePrimeDraftEntity
            {
                Id = draftId,
                SupervisorUserId = "sup",
                RootPoleId = "p1",
                CelluleId = "c1",
                Period = "2026-05",
                TemplateId = "t",
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            db.EmployeePrimeServiceFiches.Add(new EmployeePrimeServiceFicheEntity
            {
                Id = Guid.NewGuid(),
                CellulePrimeDraftId = draftId,
                EmployeeId = "e1",
                ServiceId = "s1",
                CelluleId = "c1",
                Period = "2026-05",
                FillingStatus = "Complete",
                ValidationStatus = "Terminal OK",
                SupervisorUserId = "sup",
                ServiceSaisieJson = "{}",
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();

            db.WorkflowSteps.Add(new WorkflowStepConfigEntity
            {
                Id = Guid.NewGuid(),
                ApproverRole = "Chef de projet",
                FromStatus = "Pending",
                ToStatus = "Terminal OK",
                SortOrder = 2,
                IsActive = true,
                TerminalApproved = true,
            });
            await db.SaveChangesAsync();

            var svc = (await readiness.GetReadinessAsync("2026-05")).Services.Single(s => s.ServiceId == "s1");
            Assert.True(svc.Ready);
            Assert.True(await readiness.IsScopeReadyAsync("2026-05", GlobalPoolScopeTypes.Service, "s1"));
        }
    }
}
