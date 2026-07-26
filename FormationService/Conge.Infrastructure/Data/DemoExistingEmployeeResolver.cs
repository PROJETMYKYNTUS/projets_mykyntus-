using Conge.Domain.Entities;
using Conge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Conge.Infrastructure.Data;

/// <summary>
/// Résout les SubjectId / emails démo vers des <see cref="EmployeSnapshot"/> déjà projetés.
/// Si absent : crée uniquement un snapshot minimal (pas d'employé Planning / Directory).
/// </summary>
public static class DemoExistingEmployeeResolver
{
    public sealed record DemoEmployee(
        Guid SubjectId,
        string Email,
        string Nom,
        string Prenom,
        string Role,
        Guid ManagerId);

    private static readonly Guid DefaultManagerId = Guid.Parse("11111111-1111-4111-8111-111111111105");
    private static readonly Guid DefaultServiceId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa01");

    /// <summary>Catalogue Auth démo (SubjectId stables) — ne crée jamais d'user Planning.</summary>
    public static IReadOnlyList<DemoEmployee> Catalog { get; } =
    [
        new(Guid.Parse("11111111-1111-4111-8111-111111111103"), "employee@kyntus.ma", "El Idrissi", "Yasmine", "Employee", DefaultManagerId),
        new(Guid.Parse("11111111-1111-4111-8111-111111111101"), "yasmine.elidrissi@contactcentre.ma", "El Idrissi", "Yasmine", "Employee", DefaultManagerId),
        new(Guid.Parse("11111111-1111-4111-8111-111111111105"), "manager@kyntus.ma", "Benchrif", "Nadia", "Manager", Guid.Empty),
        new(Guid.Parse("11111111-1111-4111-8111-111111111106"), "coach@kyntus.ma", "Tazi", "Omar", "Coach", DefaultManagerId),
        new(Guid.Parse("11111111-1111-4111-8111-111111111107"), "rp@kyntus.ma", "Benkirane", "Ghita", "RP", DefaultManagerId),
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"), "superviseur@kyntus.ma", "Alami", "Kenza", "Superviseur", DefaultManagerId),
        new(Guid.Parse("11111111-1111-4111-8111-111111111104"), "rh@kyntus.ma", "Mansouri", "Latifa", "RH", DefaultManagerId),
    ];

    public static async Task EnsureMinimalSnapshotsAsync(
        CongeDbContext db,
        IEnumerable<Guid> subjectIds,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        var wanted = subjectIds.ToHashSet();
        var added = 0;
        foreach (var demo in Catalog.Where(d => wanted.Contains(d.SubjectId)))
        {
            if (await db.EmployeSnapshots.AnyAsync(e => e.EmployeId == demo.SubjectId, ct))
                continue;

            await db.EmployeSnapshots.AddAsync(
                EmployeSnapshot.Creer(
                    demo.SubjectId,
                    demo.Nom,
                    demo.Prenom,
                    demo.Email,
                    demo.ManagerId == Guid.Empty ? demo.SubjectId : demo.ManagerId,
                    DefaultServiceId,
                    "Contact centre (snapshot démo)",
                    DateTime.UtcNow.AddYears(-2),
                    estMineur: false,
                    role: demo.Role),
                ct);
            added++;
            logger?.LogInformation(
                "Conge : snapshot minimal créé pour {Email} ({SubjectId}) — event-only.",
                demo.Email,
                demo.SubjectId);
        }

        if (added > 0)
            await db.SaveChangesAsync(ct);

        foreach (var id in wanted.Where(id => Catalog.All(c => c.SubjectId != id)))
        {
            if (!await db.EmployeSnapshots.AnyAsync(e => e.EmployeId == id, ct))
                logger?.LogWarning("Conge : SubjectId {SubjectId} absent du catalogue et de la base — skip.", id);
        }
    }

    public static async Task<EmployeSnapshot?> ResolveBySubjectIdAsync(
        CongeDbContext db,
        Guid subjectId,
        CancellationToken ct = default) =>
        await db.EmployeSnapshots.FirstOrDefaultAsync(e => e.EmployeId == subjectId, ct);

    public static async Task<EmployeSnapshot?> ResolveByEmailAsync(
        CongeDbContext db,
        string email,
        CancellationToken ct = default)
    {
        var needle = email.Trim().ToLowerInvariant();
        return await db.EmployeSnapshots.FirstOrDefaultAsync(e => e.Email.ToLower() == needle, ct);
    }
}
