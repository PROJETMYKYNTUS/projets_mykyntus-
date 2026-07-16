using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Planning.Domain.Entities;

namespace Planning.Infrastructure.Persistence;

/// <summary>
/// Référentiel planning Docker : org contact centre (miroir Prime) + employés e1–e11
/// alignés Auth SubjectId. Flag KYNTUS_PLANNING_DEMO_SEED=true.
/// </summary>
internal static class DockerComposePlanningDemoSeed
{
    internal static async Task ApplyIfEnabledAsync(IConfiguration configuration, AppDbContext context)
    {
        if (!string.Equals(configuration["KYNTUS_PLANNING_DEMO_SEED"], "true", StringComparison.OrdinalIgnoreCase))
            return;

        await EnsureDemoRolesAsync(context);
        var (subC1, _) = await EnsureContactCentreOrgAsync(context);

        var roleByName = await context.Roles.ToDictionaryAsync(r => r.Name, r => r.Id);

        var pwd = BCrypt.Net.BCrypt.HashPassword(
            configuration["DemoSeed:PlanningDemoPassword"]
            ?? throw new InvalidOperationException(
                "DemoSeed:PlanningDemoPassword requis lorsque KYNTUS_PLANNING_DEMO_SEED=true."));
        var hire = DateTime.UtcNow.AddMonths(-6);

        foreach (var emp in ContactCentreRoster.Employees)
        {
            if (!roleByName.TryGetValue(emp.PlanningRole, out var roleId))
                roleId = roleByName["Pilote"];

            // RH hors cellule opérationnelle ; les autres sur c1 (sauf e5 déjà RH)
            int? subId = emp.PlanningRole is "RH" ? null : subC1.Id;

            var email = ContactCentreRoster.PlanningLoginEmail(emp);
            await UpsertUserAsync(
                context,
                emp.FirstName,
                emp.LastName,
                email,
                emp.Guid,
                roleId,
                subId,
                pwd,
                hire);
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Crée ou migre l'org Planning vers le miroir Prime (d1/p1/c1 + c2).
    /// </summary>
    private static async Task<(SubService Primary, SubService Secondary)> EnsureContactCentreOrgAsync(
        AppDbContext context)
    {
        var floor = await context.Floors
            .FirstOrDefaultAsync(f => f.PrimePoleId == "d1");
        if (floor is null)
        {
            floor = await context.Floors.OrderBy(f => f.Id).FirstOrDefaultAsync();
            if (floor is null)
            {
                floor = new Floor
                {
                    Name = "Relation client & centres d'appels — Casablanca (Maroc)",
                    FloorNumber = 1,
                    Description = "Pôle contact centre (Prime d1)",
                    PrimePoleId = "d1",
                };
                context.Floors.Add(floor);
                await context.SaveChangesAsync();
            }
            else
            {
                floor.Name = "Relation client & centres d'appels — Casablanca (Maroc)";
                floor.Description = "Pôle contact centre (Prime d1)";
                floor.PrimePoleId = "d1";
                await context.SaveChangesAsync();
            }
        }

        var service = await context.Services
            .FirstOrDefaultAsync(s => s.PrimeCelluleId == "p1" || s.Code == "p1" || s.Code == "ATLAS-DEMO");
        if (service is null)
        {
            service = new Service
            {
                FloorId = floor.Id,
                Name = "Plateforme inbound — grands comptes",
                Code = "p1",
                PrimeCelluleId = "p1",
            };
            context.Services.Add(service);
            await context.SaveChangesAsync();
        }
        else
        {
            service.FloorId = floor.Id;
            service.Name = "Plateforme inbound — grands comptes";
            service.Code = "p1";
            service.PrimeCelluleId = "p1";
            await context.SaveChangesAsync();
        }

        var subC1 = await context.SubServices
            .FirstOrDefaultAsync(s => s.PrimeServiceId == "c1" || s.Code == "c1" || s.Code == "CELL-DEMO");
        if (subC1 is null)
        {
            subC1 = new SubService
            {
                ServiceId = service.Id,
                Name = "Agents 1er niveau (voice / chat)",
                Code = "c1",
                PrimeServiceId = "c1",
            };
            context.SubServices.Add(subC1);
            await context.SaveChangesAsync();
        }
        else
        {
            subC1.ServiceId = service.Id;
            subC1.Name = "Agents 1er niveau (voice / chat)";
            subC1.Code = "c1";
            subC1.PrimeServiceId = "c1";
            await context.SaveChangesAsync();
        }

        var subC2 = await context.SubServices
            .FirstOrDefaultAsync(s => s.PrimeServiceId == "c2" || s.Code == "c2");
        if (subC2 is null)
        {
            subC2 = new SubService
            {
                ServiceId = service.Id,
                Name = "Enquêtes NPS & rappels satisfaction",
                Code = "c2",
                PrimeServiceId = "c2",
            };
            context.SubServices.Add(subC2);
            await context.SaveChangesAsync();
        }
        else
        {
            subC2.ServiceId = service.Id;
            subC2.Name = "Enquêtes NPS & rappels satisfaction";
            subC2.Code = "c2";
            subC2.PrimeServiceId = "c2";
            await context.SaveChangesAsync();
        }

        return (subC1, subC2);
    }

    private static async Task UpsertUserAsync(
        AppDbContext context,
        string first,
        string last,
        string email,
        Guid stableGuid,
        int roleId,
        int? subId,
        string passwordHash,
        DateTime hire)
    {
        var needle = email.Trim().ToLowerInvariant();
        var row = await context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == needle);

        // Si un autre user a déjà ce Guid (ex. ancien email démo), le réaligner
        var byGuid = await context.Users.FirstOrDefaultAsync(u => u.Guid == stableGuid);
        if (byGuid is not null && (row is null || byGuid.Id != row.Id))
        {
            byGuid.FirstName = first;
            byGuid.LastName = last;
            byGuid.Email = email;
            byGuid.RoleId = roleId;
            byGuid.SubServiceId = subId;
            byGuid.IsActive = true;
            if (string.IsNullOrWhiteSpace(byGuid.PasswordHash))
                byGuid.PasswordHash = passwordHash;
            return;
        }

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

        row.Guid = stableGuid;
        row.FirstName = first;
        row.LastName = last;
        row.RoleId = roleId;
        row.SubServiceId = subId;
        row.IsActive = true;
        if (string.IsNullOrWhiteSpace(row.PasswordHash))
            row.PasswordHash = passwordHash;
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
