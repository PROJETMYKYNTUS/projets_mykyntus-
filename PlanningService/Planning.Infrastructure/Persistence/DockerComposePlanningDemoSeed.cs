using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Planning.Domain.Entities;

namespace Planning.Infrastructure.Persistence;

/// <summary>
/// Référentiel planning minimal pour un clone + Docker (KYNTUS_PLANNING_DEMO_SEED=true), aligné sur les comptes AuthService.
/// </summary>
internal static class DockerComposePlanningDemoSeed
{
    /// <summary>GUID employé alignés sur Auth SubjectId / documentation (init/demo).</summary>
    private static readonly IReadOnlyDictionary<string, Guid> StableEmployeeGuids =
        new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
        {
            ["employee@kyntus.ma"] = Guid.Parse("11111111-1111-4111-8111-111111111103"),
            ["rh@kyntus.ma"] = Guid.Parse("11111111-1111-4111-8111-111111111104"),
            ["manager@kyntus.ma"] = Guid.Parse("11111111-1111-4111-8111-111111111105"),
            ["coach@kyntus.ma"] = Guid.Parse("11111111-1111-4111-8111-111111111106"),
            ["rp@kyntus.ma"] = Guid.Parse("11111111-1111-4111-8111-111111111107"),
            ["admin@kyntus.ma"] = Guid.Parse("11111111-1111-4111-8111-111111111108"),
            ["audit@kyntus.ma"] = Guid.Parse("11111111-1111-4111-8111-111111111109"),
            ["formation@kyntus.ma"] = Guid.Parse("11111111-1111-4111-8111-111111111110"),
            ["superviseur@kyntus.ma"] = Guid.Parse("11111111-1111-4111-8111-111111111111"),
        };

    internal static async Task ApplyIfEnabledAsync(IConfiguration configuration, AppDbContext context)
    {
        if (!string.Equals(configuration["KYNTUS_PLANNING_DEMO_SEED"], "true", StringComparison.OrdinalIgnoreCase))
            return;

        await EnsureDemoRolesAsync(context);
        await EnsureDemoOrgAsync(context);

        var sub = await context.SubServices.OrderBy(s => s.Id).FirstAsync();
        var roleEmployee = await context.Roles.FirstAsync(r => r.Name == "Pilote");
        var roleRh = await context.Roles.FirstAsync(r => r.Name == "RH");
        var roleManager = await context.Roles.FirstAsync(r => r.Name == "Superviseur");
        var roleCoach = await context.Roles.FirstAsync(r => r.Name == "Référent technique");
        var roleRp = await context.Roles.FirstAsync(r => r.Name == "Chef de projet");
        var roleAdmin = await context.Roles.FirstAsync(r => r.Name == "Admin");
        var roleAudit = await context.Roles.FirstAsync(r => r.Name == "Audit");
        var roleFormation = await context.Roles.FirstAsync(r => r.Name == "EquipeFormation");

        var pwd = BCrypt.Net.BCrypt.HashPassword(
            configuration["DemoSeed:PlanningDemoPassword"]
            ?? throw new InvalidOperationException(
                "DemoSeed:PlanningDemoPassword requis lorsque KYNTUS_PLANNING_DEMO_SEED=true."));
        var hire = DateTime.UtcNow.AddMonths(-6);

        await UpsertDemoUserAsync(context, "Employé", "Démo", "employee@kyntus.ma", roleEmployee.Id, sub.Id, pwd, hire);
        await UpsertDemoUserAsync(context, "Rh", "Démo", "rh@kyntus.ma", roleRh.Id, null, pwd, hire);
        await UpsertDemoUserAsync(context, "Manager", "Démo", "manager@kyntus.ma", roleManager.Id, sub.Id, pwd, hire);
        await UpsertDemoUserAsync(context, "Coach", "Démo", "coach@kyntus.ma", roleCoach.Id, sub.Id, pwd, hire);
        await UpsertDemoUserAsync(context, "Rp", "Démo", "rp@kyntus.ma", roleRp.Id, sub.Id, pwd, hire);
        await UpsertDemoUserAsync(context, "Admin", "Démo", "admin@kyntus.ma", roleAdmin.Id, sub.Id, pwd, hire);
        await UpsertDemoUserAsync(context, "Audit", "Démo", "audit@kyntus.ma", roleAudit.Id, sub.Id, pwd, hire);
        await UpsertDemoUserAsync(context, "Formation", "Démo", "formation@kyntus.ma", roleFormation.Id, sub.Id, pwd, hire);
        await UpsertDemoUserAsync(context, "Superviseur", "Démo", "superviseur@kyntus.ma", roleManager.Id, sub.Id, pwd, hire);
        await UpsertDemoUserAsync(context, "Yasmine", "El Amrani", "yasmine.elamrani@atlas-tech-demo.dev", roleEmployee.Id, sub.Id, pwd, hire);
        await UpsertDemoUserAsync(context, "Fatima", "Alaoui", "fatima.alaoui@atlas-tech-demo.dev", roleRh.Id, null, pwd, hire);

        await context.SaveChangesAsync();
    }

    private static async Task EnsureDemoOrgAsync(AppDbContext context)
    {
        if (!await context.Floors.AnyAsync())
        {
            context.Floors.Add(new Floor
            {
                Name = "Siège démo",
                FloorNumber = 1,
                Description = "Données Docker compose",
            });
            await context.SaveChangesAsync();
        }

        var floor = await context.Floors.OrderBy(f => f.Id).FirstAsync();
        if (!await context.Services.AnyAsync())
        {
            context.Services.Add(new Service
            {
                FloorId = floor.Id,
                Name = "Service démo Atlas",
                Code = "ATLAS-DEMO",
            });
            await context.SaveChangesAsync();
        }

        var service = await context.Services.OrderBy(s => s.Id).FirstAsync();
        if (!await context.SubServices.AnyAsync())
        {
            context.SubServices.Add(new SubService
            {
                ServiceId = service.Id,
                Name = "Cellule démo",
                Code = "CELL-DEMO",
            });
            await context.SaveChangesAsync();
        }
    }

    private static async Task UpsertDemoUserAsync(
        AppDbContext context,
        string first,
        string last,
        string email,
        int roleId,
        int? subId,
        string passwordHash,
        DateTime hire)
    {
        var needle = email.Trim().ToLowerInvariant();
        var row = await context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == needle);
        if (StableEmployeeGuids.TryGetValue(email, out var stableGuid))
        {
            if (row is null)
            {
                context.Users.Add(new User
                {
                    Guid = stableGuid,
                    FirstName = first,
                    LastName = last,
                    Email = email,
                    RoleId = roleId,
                    SubServiceId = subId,
                    PasswordHash = passwordHash,
                    HireDate = hire,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                });
                return;
            }

            if (row.Guid != stableGuid)
                row.Guid = stableGuid;
            if (!row.IsActive)
                row.IsActive = true;
            return;
        }

        if (row is not null)
            return;

        context.Users.Add(new User
        {
            FirstName = first,
            LastName = last,
            Email = email,
            RoleId = roleId,
            SubServiceId = subId,
            PasswordHash = passwordHash,
            HireDate = hire,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
    }

    private static async Task EnsureDemoRolesAsync(AppDbContext context)
    {
        var utc = DateTime.UtcNow;
        var specs = new (string Name, string Description)[]
        {
            ("Pilote", "Pilote"),
            ("RH", "Ressources humaines"),
            ("Superviseur", "Superviseur de cellule"),
            ("Référent technique", "Référent technique"),
            ("Chef de projet", "Chef de projet"),
            ("Admin", "Administrateur"),
            ("Audit", "Audit"),
            ("EquipeFormation", "Équipe formation"),
        };

        foreach (var (name, description) in specs)
        {
            if (await context.Roles.AnyAsync(r => r.Name == name))
                continue;

            context.Roles.Add(new Role
            {
                Name = name,
                Description = description,
                IsActive = true,
                CreatedAt = utc,
            });
        }

        await context.SaveChangesAsync();
    }
}
