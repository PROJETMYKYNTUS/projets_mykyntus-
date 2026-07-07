using Conge.Domain.Entities;
using Conge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Conge.Infrastructure.Data;

/// <summary>
/// Annuaire employé minimal pour Docker (aligné sur AuthService SubjectId / JWT <c>sub</c>).
/// </summary>
public static class DockerComposeCongeDemoSeed
{
    private static readonly (Guid EmployeId, string Nom, string Prenom, string Email, string Role)[] DemoEmployes =
    [
        (Guid.Parse("11111111-1111-4111-8111-111111111103"), "Démo", "Employé", "employee@kyntus.ma", "Pilote"),
        (Guid.Parse("11111111-1111-4111-8111-111111111104"), "Démo", "Rh", "rh@kyntus.ma", "RH"),
        (Guid.Parse("11111111-1111-4111-8111-111111111105"), "Démo", "Manager", "manager@kyntus.ma", "Superviseur"),
        (Guid.Parse("11111111-1111-4111-8111-111111111106"), "Démo", "Coach", "coach@kyntus.ma", "Référent technique"),
        (Guid.Parse("11111111-1111-4111-8111-111111111107"), "Démo", "Rp", "rp@kyntus.ma", "Chef de projet"),
        (Guid.Parse("11111111-1111-4111-8111-111111111108"), "Démo", "Admin", "admin@kyntus.ma", "Admin"),
        (Guid.Parse("11111111-1111-4111-8111-111111111109"), "Démo", "Audit", "audit@kyntus.ma", "Audit"),
        (Guid.Parse("11111111-1111-4111-8111-111111111110"), "Démo", "Formation", "formation@kyntus.ma", "RH"),
        (Guid.Parse("11111111-1111-4111-8111-111111111111"), "Démo", "Superviseur", "superviseur@kyntus.ma", "Superviseur"),
        (Guid.Parse("11111111-1111-4111-8111-111111111101"), "El Amrani", "Yasmine", "yasmine.elamrani@atlas-tech-demo.dev", "Pilote"),
        (Guid.Parse("11111111-1111-4111-8111-111111111102"), "Alaoui", "Fatima", "fatima.alaoui@atlas-tech-demo.dev", "RH"),
    ];

    public static async Task ApplyIfEnabledAsync(
        IConfiguration configuration,
        CongeDbContext db,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        if (!string.Equals(configuration["KYNTUS_CONGE_DEMO_SEED"], "true", StringComparison.OrdinalIgnoreCase))
            return;

        var managerId = Guid.Parse("11111111-1111-4111-8111-111111111105");
        var serviceId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var hire = DateTime.UtcNow.AddYears(-2);

        foreach (var row in DemoEmployes)
        {
            var exists = await db.EmployeSnapshots.AnyAsync(e => e.EmployeId == row.EmployeId, ct);
            if (exists)
                continue;

            var mgrId = row.Role is "RH" or "Admin" or "Audit" ? row.EmployeId : managerId;
            await db.EmployeSnapshots.AddAsync(
                EmployeSnapshot.Creer(
                    row.EmployeId,
                    row.Nom,
                    row.Prenom,
                    row.Email,
                    mgrId,
                    serviceId,
                    "Service démo",
                    hire,
                    estMineur: false,
                    role: row.Role),
                ct);
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(ct);
            logger?.LogInformation("Conge demo seed : {Count} snapshots employé insérés.", DemoEmployes.Length);
        }
    }
}
