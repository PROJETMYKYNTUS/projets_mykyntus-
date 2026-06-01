using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Services;
using Xunit;

namespace PrimeBackend.Tests;

public sealed class PrimeSynthesisTrackingFeedTests
{
    private static (PrimeDbContext Db, SqliteConnection Conn, PrimeFicheValidationHistoryService History, PrimeRbacReadService Rbac) Create()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var opts = new DbContextOptionsBuilder<PrimeDbContext>().UseSqlite(conn).Options;
        var db = new PrimeDbContext(opts);
        db.Database.EnsureCreated();
        var history = new PrimeFicheValidationHistoryService(db);
        var rbac = new PrimeRbacReadService(db, new PrimeOrgScopeService(db));
        return (db, conn, history, rbac);
    }

    private static async Task<(Guid FicheId, Guid SynId, Guid LineId)> SeedSynthesisGraphAsync(PrimeDbContext db)
    {
        var draftId = Guid.NewGuid();
        var ficheId = Guid.NewGuid();
        var synId = Guid.NewGuid();
        var lineId = Guid.NewGuid();

        db.Poles.Add(new PoleEntity { Id = "p1", Name = "Pôle" });
        db.Cellules.Add(new CelluleEntity { Id = "c1", Name = "Cell", PoleId = "p1" });
        db.Services.Add(new ServiceEntity { Id = "s1", Name = "Svc", CelluleId = "c1" });
        db.Employees.Add(new EmployeeEntity
        {
            Id = "e1",
            FirstName = "Emp",
            LastName = "Loyé",
            Role = "Pilote",
            ServiceId = "s1",
            CelluleId = "c1",
            PoleId = "p1",
            Email = "e@test.c",
        });
        db.Employees.Add(new EmployeeEntity
        {
            Id = "rh1",
            FirstName = "Rh",
            LastName = "User",
            Role = "RH",
            ServiceId = "s1",
            CelluleId = "c1",
            PoleId = "p1",
            Email = "rh@test.c",
        });
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
            Id = ficheId,
            CellulePrimeDraftId = draftId,
            EmployeeId = "e1",
            ServiceId = "s1",
            CelluleId = "c1",
            Period = "2026-05",
            FillingStatus = "Complete",
            ValidationStatus = "RhApproved",
            SupervisorUserId = "sup",
            ServiceSaisieJson = "{}",
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        db.GlobalPoolScopeSyntheses.Add(new GlobalPoolScopeSynthesisEntity
        {
            Id = synId,
            Period = "2026-05",
            ScopeType = GlobalPoolScopeTypes.Service,
            ScopeId = "s1",
            ScopeDisplayName = "Svc",
            ExcelContent = [1, 2, 3],
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        db.GlobalPoolSynthesisLines.Add(new GlobalPoolSynthesisLineEntity
        {
            Id = lineId,
            ScopeSynthesisId = synId,
            FicheId = ficheId,
            LineStatus = GlobalPoolSynthesisLineStatuses.PendingReview,
            RhDecision = GlobalPoolLineDecisions.Pending,
            ManagerDecision = GlobalPoolLineDecisions.Pending,
        });
        await db.SaveChangesAsync();
        return (ficheId, synId, lineId);
    }

    [Fact]
    public async Task ListSynthesisTrackingFeed_returns_pool_events_only()
    {
        var (db, conn, history, rbac) = Create();
        using (conn)
        {
            var (ficheId, _, lineId) = await SeedSynthesisGraphAsync(db);
            db.EmployeePrimeFicheValidationHistories.Add(new EmployeePrimeFicheValidationHistoryEntity
            {
                Id = Guid.NewGuid(),
                FicheId = ficheId,
                At = DateTimeOffset.UtcNow.AddHours(-2),
                Action = PrimeFicheValidationHistoryActions.Approved,
                FromStatus = "Pending",
                ToStatus = "Superviseur Approved",
                ActorUserId = "sup",
                ActorRole = "Superviseur",
                ActorDisplayName = "Superviseur",
            });
            db.GlobalPoolSynthesisLineHistories.Add(new GlobalPoolSynthesisLineHistoryEntity
            {
                Id = Guid.NewGuid(),
                LineId = lineId,
                At = DateTimeOffset.UtcNow.AddHours(-1),
                Action = GlobalPoolSynthesisLineHistoryActions.Approved,
                ActorUserId = "rh1",
                ActorRole = "RH",
            });
            await db.SaveChangesAsync();

            var feed = await history.ListSynthesisTrackingFeedAsync(null, rbac, "2026-05", false, null, 100);

            Assert.All(feed, item => Assert.NotEqual("Fiche", item.Phase));
            Assert.Contains(feed, item => item.Phase == "GlobalPool" && item.Action == "Approved");
            Assert.DoesNotContain(feed, item => item.FromStatus == "Pending" && item.ToStatus == "Superviseur Approved");
        }
    }

    [Fact]
    public async Task ListSynthesisLineHistory_returns_ordered_events()
    {
        var (db, conn, history, rbac) = Create();
        using (conn)
        {
            var (_, _, lineId) = await SeedSynthesisGraphAsync(db);
            db.GlobalPoolSynthesisLines.Single(l => l.Id == lineId).LineStatus =
                GlobalPoolSynthesisLineStatuses.Approved;
            var t1 = DateTimeOffset.UtcNow.AddHours(-2);
            var t2 = DateTimeOffset.UtcNow.AddHours(-1);
            db.GlobalPoolSynthesisLineHistories.Add(new GlobalPoolSynthesisLineHistoryEntity
            {
                Id = Guid.NewGuid(),
                LineId = lineId,
                At = t1,
                Action = GlobalPoolSynthesisLineHistoryActions.Approved,
                ActorUserId = "rh1",
                ActorRole = "RH",
            });
            db.GlobalPoolSynthesisLineHistories.Add(new GlobalPoolSynthesisLineHistoryEntity
            {
                Id = Guid.NewGuid(),
                LineId = lineId,
                At = t2,
                Action = GlobalPoolSynthesisLineHistoryActions.Paid,
                ActorUserId = "rh1",
                ActorRole = "RH",
            });
            await db.SaveChangesAsync();

            var rows = await history.ListSynthesisLineHistoryAsync(lineId, null, rbac);

            Assert.Equal(2, rows.Count);
            Assert.True(rows[0].At <= rows[1].At);
            Assert.Equal(GlobalPoolSynthesisLineHistoryActions.Approved, rows[0].Action);
            Assert.Equal(GlobalPoolSynthesisLineHistoryActions.Paid, rows[1].Action);
        }
    }
}
