using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;
using PrimeBackend.Services;
using Xunit;

namespace PrimeBackend.Tests;

public sealed class PrimeDeletionGuardTests
{
    private static (PrimeDbContext Db, SqliteConnection Conn, PrimeDeletionGuardService Guard) Create()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var opts = new DbContextOptionsBuilder<PrimeDbContext>().UseSqlite(conn).Options;
        var db = new PrimeDbContext(opts);
        db.Database.EnsureCreated();
        var wfRuntime = new PrimeValidationWorkflowRuntime(db);
        var guard = new PrimeDeletionGuardService(db, wfRuntime);
        return (db, conn, guard);
    }

    private static async Task<(Guid DraftId, Guid FicheId)> SeedDraftWithPilotAsync(
        PrimeDbContext db,
        string validationStatus = PrimeValidationWorkflowService.AwaitingData,
        DateTimeOffset? frozenAt = null,
        bool withGlobalPool = false)
    {
        var draftId = Guid.NewGuid();
        var ficheId = Guid.NewGuid();
        db.Poles.Add(new PoleEntity { Id = "p1", Name = "Pôle" });
        db.Cellules.Add(new CelluleEntity { Id = "c1", Name = "Cell", PoleId = "p1" });
        db.SupervisorCellulePrimeDrafts.Add(new SupervisorCellulePrimeDraftEntity
        {
            Id = draftId,
            SupervisorUserId = "sup",
            RootPoleId = "p1",
            CelluleId = "c1",
            Period = "2026-05",
            TemplateId = "tpl1",
            Status = "Draft",
            SchemaJson = "{}",
            CelluleSaisieJson = "{}",
            UpdatedAt = DateTimeOffset.UtcNow,
            GlobalPoolExcelContent = withGlobalPool ? [1, 2, 3] : null,
            GlobalPoolUploadedAt = withGlobalPool ? DateTimeOffset.UtcNow : null,
        });
        db.EmployeePrimeServiceFiches.Add(new EmployeePrimeServiceFicheEntity
        {
            Id = ficheId,
            CellulePrimeDraftId = draftId,
            EmployeeId = "emp1",
            ServiceId = "s1",
            CelluleId = "c1",
            Period = "2026-05",
            SupervisorUserId = "sup",
            FillingStatus = "InProgress",
            ValidationStatus = validationStatus,
            ServiceSaisieJson = """{"indicator":"Taux de service"}""",
            DetailGridFrozenAt = frozenAt,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return (draftId, ficheId);
    }

    [Fact]
    public async Task CanDeleteCommonsDraft_allows_AwaitingData_without_frozen_snapshot()
    {
        var (db, conn, guard) = Create();
        using (conn)
        {
            var (draftId, _) = await SeedDraftWithPilotAsync(db);
            var (canDelete, reason, impact) = await guard.CanDeleteCommonsDraftAsync(draftId);
            Assert.True(canDelete);
            Assert.Null(reason);
            Assert.Equal(1, impact.DeletablePilotCount);
            Assert.Equal(0, impact.BlockedPilotCount);
        }
    }

    [Fact]
    public async Task CanDeleteCommonsDraft_blocks_Pending()
    {
        var (db, conn, guard) = Create();
        using (conn)
        {
            var (draftId, _) = await SeedDraftWithPilotAsync(db, PrimeValidationWorkflowService.Pending);
            var (canDelete, reason, impact) = await guard.CanDeleteCommonsDraftAsync(draftId);
            Assert.False(canDelete);
            Assert.NotNull(reason);
            Assert.Equal(1, impact.InWorkflowCount);
        }
    }

    [Fact]
    public async Task CanDeleteCommonsDraft_blocks_HistoricalImport()
    {
        var (db, conn, guard) = Create();
        using (conn)
        {
            var (draftId, _) = await SeedDraftWithPilotAsync(db, PrimeValidationWorkflowService.HistoricalImport);
            var (canDelete, reason, impact) = await guard.CanDeleteCommonsDraftAsync(draftId);
            Assert.False(canDelete);
            Assert.NotNull(reason);
            Assert.Equal(1, impact.TerminalCount);
        }
    }

    [Fact]
    public async Task CanDeleteCommonsDraft_blocks_frozen_snapshot()
    {
        var (db, conn, guard) = Create();
        using (conn)
        {
            var (draftId, _) = await SeedDraftWithPilotAsync(
                db,
                PrimeValidationWorkflowService.AwaitingData,
                frozenAt: DateTimeOffset.UtcNow);
            var (canDelete, reason, impact) = await guard.CanDeleteCommonsDraftAsync(draftId);
            Assert.False(canDelete);
            Assert.NotNull(reason);
            Assert.Equal(1, impact.FrozenCount);
        }
    }

    [Fact]
    public async Task CanDeleteCommonsDraft_blocks_global_pool_activity()
    {
        var (db, conn, guard) = Create();
        using (conn)
        {
            var (draftId, _) = await SeedDraftWithPilotAsync(db, withGlobalPool: true);
            var (canDelete, reason, impact) = await guard.CanDeleteCommonsDraftAsync(draftId);
            Assert.False(canDelete);
            Assert.NotNull(reason);
            Assert.True(impact.HasGlobalPool);
        }
    }

    [Fact]
    public void CanHardDeleteTemplate_requires_no_references_and_no_frozen()
    {
        var ok = new PrimeFicheTemplateUsageDto
        {
            TemplateId = "t1",
            CommonsDraftCount = 0,
            PilotFicheCount = 0,
            FrozenPilotFicheCount = 0,
        };
        Assert.True(PrimeDeletionGuardService.CanHardDeleteTemplate(ok));
        Assert.Equal("hardDelete", ok.RecommendedAction);

        var archive = new PrimeFicheTemplateUsageDto
        {
            TemplateId = "t2",
            CommonsDraftCount = 1,
            PilotFicheCount = 2,
            FrozenPilotFicheCount = 0,
        };
        Assert.False(PrimeDeletionGuardService.CanHardDeleteTemplate(archive));
        Assert.Equal("archive", archive.RecommendedAction);
    }

    [Fact]
    public async Task IsIndicatorProtectedByFichesAsync_true_when_referenced_in_protected_fiche()
    {
        var (db, conn, guard) = Create();
        using (conn)
        {
            await SeedDraftWithPilotAsync(db, PrimeValidationWorkflowService.Pending);
            var indicator = new ServicePrimeIndicatorEntity
            {
                Id = Guid.NewGuid(),
                ServiceId = "s1",
                Label = "Taux de service",
                SortOrder = 1,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            var protectedRef = await guard.IsIndicatorProtectedByFichesAsync(indicator, "s1");
            Assert.True(protectedRef);
        }
    }
}
