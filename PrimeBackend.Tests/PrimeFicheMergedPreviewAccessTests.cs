using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Services;
using Xunit;

namespace PrimeBackend.Tests;

public sealed class PrimeFicheMergedPreviewAccessTests
{
    private static (
        PrimeDbContext Db,
        SqliteConnection Conn,
        PrimeFicheMergedPreviewAccessService Preview,
        PrimeRbacReadService Rbac) Create()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var opts = new DbContextOptionsBuilder<PrimeDbContext>().UseSqlite(conn).Options;
        var db = new PrimeDbContext(opts);
        db.Database.EnsureCreated();
        var org = new PrimeOrgScopeService(db);
        var wfRuntime = new PrimeValidationWorkflowRuntime(db);
        var submission = new PrimeFicheValidationSubmissionService(db, wfRuntime, org);
        var rbac = new PrimeRbacReadService(db, org);
        var poolWf = new GlobalPoolWorkflowService(db);
        var preview = new PrimeFicheMergedPreviewAccessService(db, rbac, org, submission, poolWf);
        return (db, conn, preview, rbac);
    }

    private static void SeedRbac(PrimeDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        db.RbacPermissions.AddRange(
            new RbacPermissionEntity { Id = Guid.NewGuid(), Role = "RH", Action = "Read", Scope = "Global", IsAllowed = true, CreatedAt = now },
            new RbacPermissionEntity { Id = Guid.NewGuid(), Role = "Référent technique", Action = "Read", Scope = "Service", IsAllowed = true, CreatedAt = now },
            new RbacPermissionEntity { Id = Guid.NewGuid(), Role = "Référent technique", Action = "Validate", Scope = "Service", IsAllowed = true, CreatedAt = now },
            new RbacPermissionEntity { Id = Guid.NewGuid(), Role = "Pilote", Action = "Read", Scope = "Self", IsAllowed = true, CreatedAt = now });
    }

    private static async Task<(Guid FicheId, EmployeeEntity Referent, EmployeeEntity Rh, EmployeeEntity Pilote)> SeedFicheGraphAsync(
        PrimeDbContext db,
        string fillingStatus = "Complete")
    {
        var draftId = Guid.NewGuid();
        var ficheId = Guid.NewGuid();
        var synId = Guid.NewGuid();

        db.Poles.Add(new PoleEntity { Id = "p1", Name = "Pôle" });
        db.Cellules.Add(new CelluleEntity { Id = "c1", Name = "Cell", PoleId = "p1" });
        db.Services.Add(new ServiceEntity { Id = "s1", Name = "Svc", CelluleId = "c1" });
        db.Employees.Add(new EmployeeEntity
        {
            Id = "pil1",
            FirstName = "Pil",
            LastName = "Ote",
            Role = "Pilote",
            ServiceId = "s1",
            CelluleId = "c1",
            PoleId = "p1",
            Email = "p@test.c",
            ParentId = "ref1",
        });
        db.Employees.Add(new EmployeeEntity
        {
            Id = "ref1",
            FirstName = "Ref",
            LastName = "Tech",
            Role = "Référent technique",
            ServiceId = "s1",
            CelluleId = "c1",
            PoleId = "p1",
            Email = "ref@test.c",
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
        db.Employees.Add(new EmployeeEntity
        {
            Id = "other",
            FirstName = "Other",
            LastName = "User",
            Role = "Invité",
            ServiceId = "s1",
            CelluleId = "c1",
            PoleId = "p1",
            Email = "o@test.c",
        });
        db.SupervisorCellulePrimeDrafts.Add(new SupervisorCellulePrimeDraftEntity
        {
            Id = draftId,
            SupervisorUserId = "sup",
            RootPoleId = "p1",
            CelluleId = "c1",
            Period = "2026-05",
            TemplateId = "tpl1",
            Status = "Validated",
            SchemaJson = """{"rows":[]}""",
            CelluleSaisieJson = "{}",
            TemplateCalcSnapshotJson = """{"previewSheetName":"Synthèse","calcSheets":[]}""",
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        db.EmployeePrimeServiceFiches.Add(new EmployeePrimeServiceFicheEntity
        {
            Id = ficheId,
            CellulePrimeDraftId = draftId,
            EmployeeId = "pil1",
            ServiceId = "s1",
            CelluleId = "c1",
            Period = "2026-05",
            FillingStatus = fillingStatus,
            ValidationStatus = "Pending",
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
            ExcelContent = [1],
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        db.GlobalPoolSynthesisLines.Add(new GlobalPoolSynthesisLineEntity
        {
            Id = Guid.NewGuid(),
            ScopeSynthesisId = synId,
            FicheId = ficheId,
            LineStatus = GlobalPoolSynthesisLineStatuses.PendingReview,
        });
        await db.SaveChangesAsync();

        var referent = await db.Employees.FirstAsync(e => e.Id == "ref1");
        var rh = await db.Employees.FirstAsync(e => e.Id == "rh1");
        var pilote = await db.Employees.FirstAsync(e => e.Id == "pil1");
        return (ficheId, referent, rh, pilote);
    }

    private static PrimeResolvedUser Ru(EmployeeEntity e) =>
        new(e.Id, e.Role, e);

    [Fact]
    public async Task CanAccess_allows_referent_on_service_fiche()
    {
        var (db, conn, preview, _) = Create();
        using (conn)
        {
            SeedRbac(db);
            var (ficheId, referent, _, _) = await SeedFicheGraphAsync(db);
            var fiche = await db.EmployeePrimeServiceFiches.FirstAsync(f => f.Id == ficheId);
            Assert.True(await preview.CanAccessMergedPreviewAsync(Ru(referent), fiche));
        }
    }

    [Fact]
    public async Task CanAccess_allows_rh_when_fiche_in_synthesis()
    {
        var (db, conn, preview, _) = Create();
        using (conn)
        {
            SeedRbac(db);
            var (ficheId, _, rh, _) = await SeedFicheGraphAsync(db);
            var fiche = await db.EmployeePrimeServiceFiches.FirstAsync(f => f.Id == ficheId);
            Assert.True(await preview.CanAccessMergedPreviewAsync(Ru(rh), fiche));
        }
    }

    [Fact]
    public async Task CanAccess_denies_unrelated_user()
    {
        var (db, conn, preview, _) = Create();
        using (conn)
        {
            SeedRbac(db);
            var (ficheId, _, _, _) = await SeedFicheGraphAsync(db);
            var fiche = await db.EmployeePrimeServiceFiches.FirstAsync(f => f.Id == ficheId);
            var other = await db.Employees.FirstAsync(e => e.Id == "other");
            Assert.False(await preview.CanAccessMergedPreviewAsync(Ru(other), fiche));
        }
    }

    [Fact]
    public async Task BuildContext_marks_incomplete_fiche_unavailable()
    {
        var (db, conn, preview, _) = Create();
        using (conn)
        {
            SeedRbac(db);
            var (ficheId, referent, _, _) = await SeedFicheGraphAsync(db, fillingStatus: "InProgress");
            var fiche = await db.EmployeePrimeServiceFiches.FirstAsync(f => f.Id == ficheId);
            Assert.True(await preview.CanAccessMergedPreviewAsync(Ru(referent), fiche));
            var ctx = await preview.BuildContextAsync(fiche);
            Assert.NotNull(ctx);
            Assert.False(ctx!.PreviewAvailable);
            Assert.Contains("non complète", ctx.PreviewUnavailableReason ?? "", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static async Task SetLineStatusAsync(PrimeDbContext db, Guid ficheId, string status)
    {
        var line = await db.GlobalPoolSynthesisLines.FirstAsync(l => l.FicheId == ficheId);
        line.LineStatus = status;
        await db.SaveChangesAsync();
    }

    [Theory]
    [InlineData("Approved", true)]
    [InlineData("PendingReview", false)]
    [InlineData("LineRejected", false)]
    public async Task FicheApprovedByBothWorkflows_reflects_line_status(string status, bool expected)
    {
        var (db, conn, preview, _) = Create();
        using (conn)
        {
            SeedRbac(db);
            var (ficheId, _, _, _) = await SeedFicheGraphAsync(db);
            await SetLineStatusAsync(db, ficheId, status);
            var fiche = await db.EmployeePrimeServiceFiches.FirstAsync(f => f.Id == ficheId);
            Assert.Equal(expected, await preview.FicheApprovedByBothWorkflowsAsync(fiche, default));
        }
    }

    [Fact]
    public async Task CanAccess_pilote_only_when_line_approved()
    {
        var (db, conn, preview, _) = Create();
        using (conn)
        {
            SeedRbac(db);
            var (ficheId, _, _, pilote) = await SeedFicheGraphAsync(db);
            var fiche = await db.EmployeePrimeServiceFiches.FirstAsync(f => f.Id == ficheId);
            Assert.False(await preview.CanAccessMergedPreviewAsync(Ru(pilote), fiche));

            await SetLineStatusAsync(db, ficheId, GlobalPoolSynthesisLineStatuses.Approved);
            Assert.True(await preview.CanAccessMergedPreviewAsync(Ru(pilote), fiche));
        }
    }

    [Fact]
    public async Task BuildContext_available_when_complete_and_snapshot_present()
    {
        var (db, conn, preview, _) = Create();
        using (conn)
        {
            SeedRbac(db);
            var (ficheId, _, rh, _) = await SeedFicheGraphAsync(db);
            var fiche = await db.EmployeePrimeServiceFiches.FirstAsync(f => f.Id == ficheId);
            var ctx = await preview.BuildContextAsync(fiche);
            Assert.NotNull(ctx);
            Assert.True(ctx!.PreviewAvailable);
            Assert.Equal("tpl1", ctx.TemplateId);
            Assert.NotEmpty(ctx.TemplateCalcSnapshotJson ?? "");
        }
    }
}
