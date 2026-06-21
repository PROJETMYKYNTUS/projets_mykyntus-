using Microsoft.EntityFrameworkCore;

namespace PrimeBackend.Data;

public static class AllowanceDbSeeder
{
    public static async Task SeedAsync(PrimeDbContext db, CancellationToken ct = default)
    {
        await SeedTypesAsync(db, ct);
        await SeedWorkflowAsync(db, ct);
        await SeedAutoRulesAsync(db, ct);
    }

    private static async Task SeedTypesAsync(PrimeDbContext db, CancellationToken ct)
    {
        if (await db.AllowanceTypes.AnyAsync(ct)) return;
        var now = DateTimeOffset.UtcNow;
        var types = new (string Code, string Label, string Category, bool Justification, decimal? Max)[]
        {
            ("PERF_INDIV", "Prime performance individuelle", "Performance", false, 10000),
            ("PERF_PROJECT", "Prime réalisation projet", "Performance", true, 15000),
            ("PERF_QUALITY", "Prime qualité", "Performance", false, 5000),
            ("HOURS_OT", "Heures supplémentaires", "Temps", false, 8000),
            ("HOURS_NIGHT", "Prime de nuit", "Temps", false, 6000),
            ("ATTENDANCE", "Prime d'assiduité", "Temps", false, 2000),
            ("ON_CALL", "Astreinte / permanence", "Temps", true, 3000),
            ("ACH_CERTIF", "Certification obtenue", "Achievement", true, 5000),
            ("ACH_MILESTONE", "Jalon atteint", "Achievement", true, 8000),
            ("ACH_EXCELLENCE", "Excellence / reconnaissance", "Achievement", true, 5000),
            ("ALLOW_TRANSPORT", "Indemnité transport", "Indemnité", false, 1500),
            ("ALLOW_TRAVEL", "Indemnité déplacement", "Indemnité", true, 5000),
            ("DISC_MANAGER", "Prime discrétionnaire manager", "Discrétionnaire", true, 5000),
            ("DISC_EXCEPTIONAL", "Contribution exceptionnelle", "Discrétionnaire", true, 10000),
            ("PERIOD_YEAR_END", "Prime de fin d'année", "Périodique", false, 20000),
        };

        foreach (var t in types)
        {
            db.AllowanceTypes.Add(new AllowanceTypeEntity
            {
                Id = Guid.NewGuid(),
                Code = t.Code,
                Label = t.Label,
                Category = t.Category,
                CalculationMode = "Manual",
                DefaultAmount = t.Code is "HOURS_OT" or "HOURS_NIGHT" or "ATTENDANCE" ? t.Max : null,
                MaxAmount = t.Max,
                RequiresJustification = t.Justification,
                ApplicableDepartmentKinds = "Support",
                IsActive = true,
                CreatedAt = now,
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedWorkflowAsync(PrimeDbContext db, CancellationToken ct)
    {
        if (await db.AllowanceWorkflowSteps.AnyAsync(ct)) return;
        var now = DateTimeOffset.UtcNow;
        db.AllowanceWorkflowSteps.AddRange(
            new AllowanceWorkflowStepEntity { Id = Guid.NewGuid(), SortOrder = 1, ApproverRole = "Manager", IsRequired = true, IsActive = true, CreatedAt = now },
            new AllowanceWorkflowStepEntity { Id = Guid.NewGuid(), SortOrder = 2, ApproverRole = "RH", IsRequired = true, IsActive = true, CreatedAt = now },
            new AllowanceWorkflowStepEntity { Id = Guid.NewGuid(), SortOrder = 3, ApproverRole = "Comptabilité", IsRequired = true, IsActive = true, CreatedAt = now });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Phase 4 — règles auto Planning/Congés par département Support (propositions Draft).</summary>
    private static async Task SeedAutoRulesAsync(PrimeDbContext db, CancellationToken ct)
    {
        if (await db.AllowanceRules.AnyAsync(ct)) return;
        var supportDepts = await db.BusinessDepartments.AsNoTracking()
            .Where(d => d.Kind == "Support" && d.IsActive)
            .ToListAsync(ct);
        if (supportDepts.Count == 0) return;

        var types = await db.AllowanceTypes.AsNoTracking()
            .Where(t => t.IsActive)
            .ToDictionaryAsync(t => t.Code, ct);
        var now = DateTimeOffset.UtcNow;
        var ruleDefs = new (string TypeCode, string DataSource, string Condition)[]
        {
            ("HOURS_OT", "Planning", "{\"minOvertimeHours\":1}"),
            ("HOURS_NIGHT", "Planning", "{\"minNightShifts\":1}"),
            ("ATTENDANCE", "Conges", "{\"maxAbsences\":0}"),
        };

        foreach (var dept in supportDepts)
        {
            foreach (var def in ruleDefs)
            {
                if (!types.TryGetValue(def.TypeCode, out var type)) continue;
                db.AllowanceRules.Add(new AllowanceRuleEntity
                {
                    Id = Guid.NewGuid(),
                    AllowanceTypeId = type.Id,
                    BusinessDepartmentId = dept.Id,
                    ConditionJson = def.Condition,
                    FormulaJson = "{\"mode\":\"defaultAmount\"}",
                    DataSource = def.DataSource,
                    IsActive = true,
                    CreatedAt = now,
                });
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
