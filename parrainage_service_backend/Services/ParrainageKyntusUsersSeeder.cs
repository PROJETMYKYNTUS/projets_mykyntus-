using Microsoft.EntityFrameworkCore;
using ParrainageBackend.Data;
using ParrainageBackend.Models;

namespace ParrainageBackend.Services;

internal static class ParrainageKyntusUsersSeeder
{
    internal static async Task SeedPortalUsersAsync(ParrainageDbContext db, ILogger logger, CancellationToken ct)
    {
        if (await db.PortalUsers.AnyAsync(ct))
            return;

        var users = new[]
        {
            Row("kyntus-employee", "employee@kyntus.ma", "Employé Démo", "PILOTE", "coach-1"),
            Row("kyntus-rh", "rh@kyntus.ma", "Rh Démo", "RH", null),
            Row("kyntus-manager", "manager@kyntus.ma", "Manager Démo", "MANAGER", "rp-1", "proj-1"),
            Row("kyntus-coach", "coach@kyntus.ma", "Coach Démo", "COACH", "mgr-1"),
            Row("kyntus-rp", "rp@kyntus.ma", "Rp Démo", "RP", null),
            Row("kyntus-admin", "admin@kyntus.ma", "Admin Démo", "ADMIN", null),
            Row("kyntus-audit", "audit@kyntus.ma", "Audit Démo", "AUDIT", null),
            Row("kyntus-formation", "formation@kyntus.ma", "Formation Démo", "RH", null),
        };

        db.PortalUsers.AddRange(users);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("PARRAINAGE : {Count} utilisateurs portail seedés.", users.Length);
    }

    private static ParrainagePortalUserEntity Row(
        string id,
        string email,
        string name,
        string role,
        string? parentId,
        string? projectId = null) =>
        new()
        {
            Id = id,
            Email = email,
            Name = name,
            Role = role,
            ParentId = parentId,
            ProjectId = projectId,
        };
}
