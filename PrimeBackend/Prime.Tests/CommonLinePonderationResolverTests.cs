using Microsoft.EntityFrameworkCore;
using Prime.Application;
using Prime.Application.DTOs;
using Prime.Domain.Entities;
using Prime.Infrastructure.Persistence;
using Prime.Infrastructure.Services;

namespace PrimeBackend.Tests;

public class CommonLinePonderationResolverTests
{
    [Fact]
    public async Task Resolve_uses_cellule_when_service_has_no_override()
    {
        await using var db = CreateDb();
        await SeedOrgAsync(db);
        var at = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        db.CommonLinePonderations.Add(Version("Cellule", "cell-1", "kpi-a", 40, 10, at));
        await db.SaveChangesAsync();

        var resolver = new CommonLinePonderationResolver(db);
        var resolved = await resolver.ResolveAsync("svc-1", "cell-1", "tpl-1", at);

        var row = Assert.Single(resolved);
        Assert.Equal(40m, row.PonderationPrimePct);
        Assert.Equal(CommonLinePonderationSources.Cellule, row.SourceScope);
        Assert.True(row.Inherited);
    }

    [Fact]
    public async Task Resolve_prefers_service_override_over_cellule()
    {
        await using var db = CreateDb();
        await SeedOrgAsync(db);
        var at = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        db.CommonLinePonderations.Add(Version("Cellule", "cell-1", "kpi-a", 40, 10, at));
        db.CommonLinePonderations.Add(Version("Service", "svc-1", "kpi-a", 55, 12, at));
        await db.SaveChangesAsync();

        var resolver = new CommonLinePonderationResolver(db);
        var resolved = await resolver.ResolveAsync("svc-1", "cell-1", "tpl-1", at);

        var row = Assert.Single(resolved);
        Assert.Equal(55m, row.PonderationPrimePct);
        Assert.Equal(CommonLinePonderationSources.Service, row.SourceScope);
        Assert.False(row.Inherited);
    }

    [Fact]
    public async Task Resolve_uses_version_active_at_date()
    {
        await using var db = CreateDb();
        await SeedOrgAsync(db);
        var jan = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var jun = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        db.CommonLinePonderations.Add(Version("Cellule", "cell-1", "kpi-a", 20, 5, jan, jun.AddTicks(-1)));
        db.CommonLinePonderations.Add(Version("Cellule", "cell-1", "kpi-a", 80, 15, jun));
        await db.SaveChangesAsync();

        var resolver = new CommonLinePonderationResolver(db);
        var before = Assert.Single(await resolver.ResolveAsync("svc-1", "cell-1", "tpl-1", jan));
        var after = Assert.Single(await resolver.ResolveAsync("svc-1", "cell-1", "tpl-1", jun));

        Assert.Equal(20m, before.PonderationPrimePct);
        Assert.Equal(80m, after.PonderationPrimePct);
    }

    [Fact]
    public async Task Resolve_prefers_previous_period_over_template()
    {
        await using var db = CreateDb();
        await SeedOrgAsync(db);
        var jun = new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero);
        var jul = new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);
        // Version cellule active en juin uniquement (fermée avant juillet).
        db.CommonLinePonderations.Add(Version("Cellule", "cell-1", "kpi-a", 40, 10, jun, jul.AddTicks(-1)));
        await db.SaveChangesAsync();

        var resolver = new CommonLinePonderationResolver(db);
        var tpl = new List<TemplateCommonLineHint>
        {
            new() { TemplateStableId = "kpi-a", Label = "A", TemplatePrimePct = 10, TemplateChallengePct = 2 },
        };
        var prev = new List<TemplateCommonLineHint>
        {
            new() { TemplateStableId = "kpi-a", Label = "A", TemplatePrimePct = 40, TemplateChallengePct = 10 },
        };

        var resolved = Assert.Single(
            await resolver.ResolveAsync("svc-1", "cell-1", "tpl-1", jul, tpl, prev));

        Assert.Equal(CommonLinePonderationSources.PreviousPeriod, resolved.SourceScope);
        Assert.Equal(40m, resolved.PonderationPrimePct);
    }

    [Fact]
    public async Task Schema_hints_parse_sector_defaults()
    {
        const string schema = """
            {"lines":[{"stableId":"rip","indicator":"Taux de report","contract":"RACC",
              "secteurs":[{"defaults":{"ponderationPrime":"12","ponderationChallenge":"3"}}]}]}
            """;
        var hints = TemplateSchemaPonderationHints.FromSchemaJson(schema);
        var row = Assert.Single(hints);
        Assert.Equal("rip", row.TemplateStableId);
        Assert.Equal(12m, row.TemplatePrimePct);
        Assert.Equal(3m, row.TemplateChallengePct);
    }

    [Fact]
    public async Task Resolve_falls_back_to_template_then_undefined()
    {
        await using var db = CreateDb();
        await SeedOrgAsync(db);
        var resolver = new CommonLinePonderationResolver(db);
        var hints = new List<TemplateCommonLineHint>
        {
            new()
            {
                TemplateStableId = "kpi-tpl",
                Label = "From template",
                Contract = "RACC",
                TemplatePrimePct = 33,
            },
            new() { TemplateStableId = "kpi-empty", Label = "Empty", Contract = "SAV" },
        };

        var resolved = await resolver.ResolveAsync(
            "svc-1", "cell-1", "tpl-1", DateTimeOffset.UtcNow, hints);

        Assert.Equal(2, resolved.Count);
        var fromTemplate = Assert.Single(resolved, x => x.TemplateStableId == "kpi-tpl");
        Assert.Equal(CommonLinePonderationSources.Template, fromTemplate.SourceScope);
        Assert.Equal(33m, fromTemplate.PonderationPrimePct);
        var empty = Assert.Single(resolved, x => x.TemplateStableId == "kpi-empty");
        Assert.Equal(CommonLinePonderationSources.Undefined, empty.SourceScope);
    }

    [Fact]
    public async Task Delete_service_override_returns_to_cellule()
    {
        await using var db = CreateDb();
        await SeedOrgAsync(db, admin: true);
        var at = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        db.CommonLinePonderations.Add(Version("Cellule", "cell-1", "kpi-a", 40, 10, at));
        db.CommonLinePonderations.Add(Version("Service", "svc-1", "kpi-a", 55, 12, at));
        await db.SaveChangesAsync();

        var resolver = new CommonLinePonderationResolver(db);
        var org = new PrimeOrgScopeService(db);
        var svc = new CommonLinePonderationsAppService(db, org, resolver);
        await svc.DeleteServiceOverrideAsync("svc-1", "kpi-a", "admin-1", "tpl-1", at);

        var resolved = await resolver.ResolveAsync("svc-1", "cell-1", "tpl-1", at);
        var row = Assert.Single(resolved);
        Assert.Equal(40m, row.PonderationPrimePct);
        Assert.Equal(CommonLinePonderationSources.Cellule, row.SourceScope);
    }

    [Fact]
    public async Task Put_closes_previous_version_without_overlap()
    {
        await using var db = CreateDb();
        await SeedOrgAsync(db, admin: true);
        var jan = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var jun = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var resolver = new CommonLinePonderationResolver(db);
        var org = new PrimeOrgScopeService(db);
        var svc = new CommonLinePonderationsAppService(db, org, resolver);

        await svc.PutCelluleAsync("cell-1", "admin-1", Body(jan, 20, 5));
        await svc.PutCelluleAsync("cell-1", "admin-1", Body(jun, 80, 15));

        var versions = await db.CommonLinePonderations
            .Where(x => x.TemplateStableId == "kpi-a")
            .OrderBy(x => x.EffectiveFrom)
            .ToListAsync();
        Assert.Equal(2, versions.Count);
        Assert.NotNull(versions[0].EffectiveTo);
        Assert.True(versions[0].EffectiveTo < versions[1].EffectiveFrom);
        Assert.Null(versions[1].EffectiveTo);
    }

    [Fact]
    public async Task Supervisor_without_scope_is_forbidden()
    {
        await using var db = CreateDb();
        await SeedOrgAsync(db);
        db.Employees.Add(new EmployeeEntity
        {
            Id = "sup-out",
            FirstName = "Out",
            LastName = "Scope",
            Role = "Superviseur",
            Email = "out@example.com",
        });
        await db.SaveChangesAsync();

        var resolver = new CommonLinePonderationResolver(db);
        var org = new PrimeOrgScopeService(db);
        var svc = new CommonLinePonderationsAppService(db, org, resolver);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.GetCelluleEffectiveAsync("cell-1", "sup-out", "tpl-1", DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Migrated_legacy_service_rows_are_resolved()
    {
        await using var db = CreateDb();
        await SeedOrgAsync(db);
        db.ServicePoleLinePonderations.Add(new ServicePoleLinePonderationEntity
        {
            Id = Guid.NewGuid(),
            ServiceId = "svc-1",
            TemplateStableId = "kpi-a",
            Label = "Legacy",
            SortOrder = 0,
            PonderationPrimePct = 42,
            PonderationChallengePct = 8,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        db.CommonLinePonderations.Add(new CommonLinePonderationEntity
        {
            Id = Guid.NewGuid(),
            ScopeType = "Service",
            ScopeId = "svc-1",
            TemplateId = "",
            TemplateStableId = "kpi-a",
            Label = "Legacy",
            SortOrder = 0,
            PonderationPrimePct = 42,
            PonderationChallengePct = 8,
            EffectiveFrom = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var resolver = new CommonLinePonderationResolver(db);
        var resolved = await resolver.ResolveAsync(
            "svc-1", "cell-1", "tpl-1", new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));
        var row = Assert.Single(resolved);
        Assert.Equal(42m, row.PonderationPrimePct);
        Assert.Equal(CommonLinePonderationSources.Service, row.SourceScope);
    }

    [Fact]
    public async Task Frozen_fiche_snapshot_ignores_later_config()
    {
        await using var db = CreateDb();
        await SeedOrgAsync(db);
        var aug = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        db.CommonLinePonderations.Add(Version("Cellule", "cell-1", "kpi-a", 40, 10, aug));
        await db.SaveChangesAsync();

        var resolver = new CommonLinePonderationResolver(db);
        var first = await resolver.ResolveAsync("svc-1", "cell-1", "tpl-1", aug);
        var fiche = new EmployeePrimeServiceFiche
        {
            ServiceId = "svc-1",
            CelluleId = "cell-1",
            Period = "2026-08",
            PonderationsSnapshotJson = CommonLinePonderationResolver.SerializeSnapshot(first, aug),
        };

        db.CommonLinePonderations.Add(Version("Cellule", "cell-1", "kpi-a", 99, 1, aug.AddMonths(1)));
        await db.SaveChangesAsync();

        var frozen = CommonLinePonderationResolver.TryParseSnapshot(fiche.PonderationsSnapshotJson);
        Assert.NotNull(frozen);
        Assert.Equal(40m, Assert.Single(frozen).PonderationPrimePct);

        var live = Assert.Single(await resolver.ResolveAsync("svc-1", "cell-1", "tpl-1", aug.AddMonths(1)));
        Assert.Equal(99m, live.PonderationPrimePct);

        // Une fiche déjà figée ne doit pas être réécrite après changement de pondération cellule.
        var snapshotBefore = fiche.PonderationsSnapshotJson;
        await resolver.FreezeOntoFicheIfMissingAsync(fiche, "tpl-1");
        Assert.Equal(snapshotBefore, fiche.PonderationsSnapshotJson);
        var stillFrozen = CommonLinePonderationResolver.TryParseSnapshot(fiche.PonderationsSnapshotJson);
        Assert.Equal(40m, Assert.Single(stillFrozen!).PonderationPrimePct);
    }

    [Fact]
    public async Task Consolidate_creates_cellule_and_closes_identical_service_overrides()
    {
        await using var db = CreateDb();
        await SeedOrgAsync(db, admin: true);
        var at = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        db.CommonLinePonderations.Add(Version("Service", "svc-1", "kpi-a", 40, 10, at));
        db.CommonLinePonderations.Add(Version("Service", "svc-2", "kpi-a", 40, 10, at));
        await db.SaveChangesAsync();

        var resolver = new CommonLinePonderationResolver(db);
        var org = new PrimeOrgScopeService(db);
        var svc = new CommonLinePonderationsAppService(db, org, resolver);
        var closed = await svc.ConsolidateIdenticalServiceOverridesAsync("cell-1", "admin-1", "tpl-1", at);

        Assert.True(closed >= 2);
        var resolved = Assert.Single(await resolver.ResolveAsync("svc-1", "cell-1", "tpl-1", at));
        Assert.Equal(40m, resolved.PonderationPrimePct);
        Assert.Equal(CommonLinePonderationSources.Cellule, resolved.SourceScope);
    }

    [Fact]
    public void Period_maps_to_first_day()
    {
        var at = CommonLinePonderationPeriod.FromPeriod("2026-08");
        Assert.Equal(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero), at);
    }

    [Fact]
    public void ForLiveResolve_is_start_of_utc_today()
    {
        var now = new DateTimeOffset(2026, 8, 17, 15, 30, 0, TimeSpan.Zero);
        Assert.Equal(new DateTimeOffset(2026, 8, 17, 0, 0, 0, TimeSpan.Zero), CommonLinePonderationPeriod.ForLiveResolve(now));
    }

    [Fact]
    public async Task Freeze_open_fiche_uses_current_weights_not_fiche_period()
    {
        await using var db = CreateDb();
        await SeedOrgAsync(db);
        var today = CommonLinePonderationPeriod.ForLiveResolve();
        var oldFrom = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        db.CommonLinePonderations.Add(Version("Cellule", "cell-1", "kpi-a", 20, 5, oldFrom, today.AddTicks(-1)));
        db.CommonLinePonderations.Add(Version("Cellule", "cell-1", "kpi-a", 80, 15, today));
        await db.SaveChangesAsync();

        var fiche = new EmployeePrimeServiceFiche
        {
            ServiceId = "svc-1",
            CelluleId = "cell-1",
            Period = "2024-07",
        };
        var resolver = new CommonLinePonderationResolver(db);
        await resolver.FreezeOntoFicheIfMissingAsync(fiche, "tpl-1");

        var frozen = CommonLinePonderationResolver.TryParseSnapshot(fiche.PonderationsSnapshotJson);
        Assert.NotNull(frozen);
        Assert.Equal(80m, Assert.Single(frozen).PonderationPrimePct);
    }

    [Fact]
    public void NormalizePct_rejects_out_of_range()
    {
        Assert.Throws<ArgumentException>(() => CommonLinePonderationPeriod.NormalizePct(101));
        Assert.Equal(12.3457m, CommonLinePonderationPeriod.NormalizePct(12.34567m));
    }

    private static PutCommonLinePonderationsRequest Body(DateTimeOffset from, decimal prime, decimal challenge) =>
        new()
        {
            TemplateId = "tpl-1",
            EffectiveFrom = from,
            Items =
            [
                new PutCommonLinePonderationItem
                {
                    TemplateStableId = "kpi-a",
                    Label = "KPI A",
                    Contract = "RACC",
                    PonderationPrimePct = prime,
                    PonderationChallengePct = challenge,
                },
            ],
        };

    private static CommonLinePonderationEntity Version(
        string scopeType,
        string scopeId,
        string stableId,
        decimal prime,
        decimal challenge,
        DateTimeOffset from,
        DateTimeOffset? to = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            ScopeType = scopeType,
            ScopeId = scopeId,
            TemplateId = "tpl-1",
            TemplateStableId = stableId,
            Label = stableId,
            Contract = "RACC",
            PonderationPrimePct = prime,
            PonderationChallengePct = challenge,
            EffectiveFrom = from,
            EffectiveTo = to,
            CreatedAt = from,
        };

    private static PrimeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<PrimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PrimeDbContext(options);
    }

    private static async Task SeedOrgAsync(PrimeDbContext db, bool admin = false)
    {
        db.Poles.Add(new PoleEntity { Id = "pole-1", Name = "Pôle" });
        db.Cellules.Add(new CelluleEntity { Id = "cell-1", Name = "Cellule", PoleId = "pole-1" });
        db.Services.Add(new ServiceEntity { Id = "svc-1", Name = "Service", CelluleId = "cell-1" });
        db.Services.Add(new ServiceEntity { Id = "svc-2", Name = "Service 2", CelluleId = "cell-1" });
        if (admin)
        {
            db.Employees.Add(new EmployeeEntity
            {
                Id = "admin-1",
                FirstName = "Ada",
                LastName = "Min",
                Role = "Admin",
                Email = "admin@example.com",
            });
        }

        await db.SaveChangesAsync();
    }
}
