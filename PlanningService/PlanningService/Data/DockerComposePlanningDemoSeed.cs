using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PlanningService.Models;

namespace PlanningService.Data;

/// <summary>
/// Référentiel planning minimal pour un clone + Docker (KYNTUS_PLANNING_DEMO_SEED=true), aligné sur les comptes AuthService.
/// </summary>
internal static class DockerComposePlanningDemoSeed
{
    internal static async Task ApplyIfEnabledAsync(IConfiguration configuration, AppDbContext context)
    {
        if (!string.Equals(configuration["KYNTUS_PLANNING_DEMO_SEED"], "true", StringComparison.OrdinalIgnoreCase))
            return;

        if (await context.Users.AnyAsync())
            return;

        // EnsureManagerRoleAsync peut déjà avoir créé « Manager » : on complète les rôles démo manquants.
        await EnsureDemoRolesAsync(context);

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

        var sub = await context.SubServices.OrderBy(s => s.Id).FirstAsync();
        var roleEmployee = await context.Roles.FirstAsync(r => r.Name == "Pilote");
        var roleRh = await context.Roles.FirstAsync(r => r.Name == "RH");
        var roleManager = await context.Roles.FirstAsync(r => r.Name == "Superviseur");
        var roleCoach = await context.Roles.FirstAsync(r => r.Name == "Référent technique");
        var roleRp = await context.Roles.FirstAsync(r => r.Name == "Chef de projet");
        var roleAdmin = await context.Roles.FirstAsync(r => r.Name == "Admin");
        var roleAudit = await context.Roles.FirstAsync(r => r.Name == "Audit");
        var roleFormation = await context.Roles.FirstAsync(r => r.Name == "EquipeFormation");

        var pwd = BCrypt.Net.BCrypt.HashPassword("KyntusDemo@2026");
        var hire = DateTime.UtcNow.AddMonths(-6);

        void AddUser(string first, string last, string email, int roleId, int? subId) =>
            context.Users.Add(new User
            {
                FirstName = first,
                LastName = last,
                Email = email,
                RoleId = roleId,
                SubServiceId = subId,
                PasswordHash = pwd,
                HireDate = hire,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });

        AddUser("Employé", "Démo", "employee@kyntus.ma", roleEmployee.Id, sub.Id);
        AddUser("Rh", "Démo", "rh@kyntus.ma", roleRh.Id, null);
        AddUser("Manager", "Démo", "manager@kyntus.ma", roleManager.Id, sub.Id);
        AddUser("Coach", "Démo", "coach@kyntus.ma", roleCoach.Id, sub.Id);
        AddUser("Rp", "Démo", "rp@kyntus.ma", roleRp.Id, sub.Id);
        AddUser("Admin", "Démo", "admin@kyntus.ma", roleAdmin.Id, sub.Id);
        AddUser("Audit", "Démo", "audit@kyntus.ma", roleAudit.Id, sub.Id);
        AddUser("Formation", "Démo", "formation@kyntus.ma", roleFormation.Id, sub.Id);
        AddUser("Yasmine", "El Amrani", "yasmine.elamrani@atlas-tech-demo.dev", roleEmployee.Id, sub.Id);
        AddUser("Fatima", "Alaoui", "fatima.alaoui@atlas-tech-demo.dev", roleRh.Id, null);

        await context.SaveChangesAsync();
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
