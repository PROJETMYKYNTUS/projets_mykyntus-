using Conge.Domain.Entities;
using Conge.Domain.Enums;
using Conge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Conge.Infrastructure.Data;

/// <summary>
/// Soldes, demandes et historique congés pour démo Docker prod-like (centre d'appels Casablanca).
/// </summary>
public static class DockerComposeCongeEnrichmentSeed
{
    private static readonly Guid ManagerId = Guid.Parse("11111111-1111-4111-8111-111111111105");
    private static readonly Guid EmployeeId = Guid.Parse("11111111-1111-4111-8111-111111111103");
    private static readonly Guid YasmineId = Guid.Parse("11111111-1111-4111-8111-111111111101");
    private static readonly Guid CoachId = Guid.Parse("11111111-1111-4111-8111-111111111106");
    private static readonly Guid RpId = Guid.Parse("11111111-1111-4111-8111-111111111107");
    private static readonly Guid SuperviseurId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    private static readonly Guid[] PiloteIds =
    [
        EmployeeId,
        YasmineId,
        CoachId,
        RpId,
        SuperviseurId,
    ];

    public static async Task ApplyIfEnabledAsync(
        IConfiguration configuration,
        CongeDbContext db,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        if (!IsEnabled(configuration))
            return;

        if (await db.DemandeConges.AnyAsync(
                d => d.Motif != null && EF.Functions.Like(d.Motif, "%(démo)%"), ct))
        {
            logger?.LogInformation("Conge enrichment déjà appliqué (demandes démo présentes).");
            return;
        }

        await DemoExistingEmployeeResolver.EnsureMinimalSnapshotsAsync(db, PiloteIds.Append(ManagerId), logger, ct);

        var annee = DateTime.UtcNow.Year;
        foreach (var employeId in PiloteIds)
        {
            if (!await db.EmployeSnapshots.AnyAsync(e => e.EmployeId == employeId, ct))
            {
                logger?.LogWarning("Conge enrichment : snapshot {EmployeId} absent — solde ignoré.", employeId);
                continue;
            }

            var hasSolde = await db.SoldeConges.AnyAsync(s => s.EmployeId == employeId && s.Annee == annee, ct);
            if (!hasSolde)
                await db.SoldeConges.AddAsync(SoldeConge.Initialiser(employeId, 18, annee), ct);
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(ct);

        if (!await db.EmployeSnapshots.AnyAsync(e => e.EmployeId == EmployeeId, ct))
        {
            logger?.LogWarning("Conge enrichment : snapshot employee démo absent — demandes ignorées.");
            return;
        }

        await SeedPendingAndDecidedDemandesAsync(db, ct);
        await SeedHistoriqueViaSqlAsync(db, ct);

        logger?.LogInformation("Conge enrichment : soldes et demandes démo insérés.");
    }

    private static bool IsEnabled(IConfiguration configuration) =>
        string.Equals(configuration["KYNTUS_CONGE_DEMO_SEED"], "true", StringComparison.OrdinalIgnoreCase)
        && string.Equals(configuration["KYNTUS_DEMO_ENRICHMENT"] ?? "false", "true", StringComparison.OrdinalIgnoreCase);

    private static async Task SeedPendingAndDecidedDemandesAsync(CongeDbContext db, CancellationToken ct)
    {
        if (!await db.EmployeSnapshots.AnyAsync(e => e.EmployeId == EmployeeId, ct))
            return;

        var employe = await db.EmployeSnapshots.FirstAsync(e => e.EmployeId == EmployeeId, ct);
        var annee = DateTime.UtcNow.Year;
        var soldeEmployee = await db.SoldeConges.FirstOrDefaultAsync(s => s.EmployeId == EmployeeId && s.Annee == annee, ct);
        if (soldeEmployee is null)
        {
            soldeEmployee = SoldeConge.Initialiser(EmployeeId, 18, annee);
            await db.SoldeConges.AddAsync(soldeEmployee, ct);
        }

        var pendingStart = NextMondayUtc().AddDays(21);
        var pendingEnd = pendingStart.AddDays(2);

        var pendingEmployee = DemandeConge.CreerCongeAnnuel(
            EmployeeId,
            ManagerId,
            pendingStart,
            pendingEnd,
            soldeEmployee,
            employe,
            "Congé familial (démo)");
        await db.DemandeConges.AddAsync(pendingEmployee, ct);

        if (await db.EmployeSnapshots.AnyAsync(e => e.EmployeId == YasmineId, ct))
        {
            var yasmine = await db.EmployeSnapshots.FirstAsync(e => e.EmployeId == YasmineId, ct);
            var soldeYasmine = await db.SoldeConges.FirstOrDefaultAsync(s => s.EmployeId == YasmineId && s.Annee == annee, ct);
            if (soldeYasmine is null)
            {
                soldeYasmine = SoldeConge.Initialiser(YasmineId, 18, annee);
                await db.SoldeConges.AddAsync(soldeYasmine, ct);
            }

            var pendingYasmineStart = NextMondayUtc().AddDays(28);
            var pendingYasmine = DemandeConge.CreerCongeAnnuel(
                YasmineId,
                ManagerId,
                pendingYasmineStart,
                pendingYasmineStart.AddDays(1),
                soldeYasmine,
                yasmine,
                "Récupération week-end (démo)");
            await db.DemandeConges.AddAsync(pendingYasmine, ct);
        }

        if (await db.EmployeSnapshots.AnyAsync(e => e.EmployeId == CoachId, ct))
        {
            var coach = await db.EmployeSnapshots.FirstAsync(e => e.EmployeId == CoachId, ct);
            var soldeCoach = await db.SoldeConges.FirstOrDefaultAsync(s => s.EmployeId == CoachId && s.Annee == annee, ct);
            if (soldeCoach is null)
            {
                soldeCoach = SoldeConge.Initialiser(CoachId, 18, annee);
                await db.SoldeConges.AddAsync(soldeCoach, ct);
            }

            var pendingCoachStart = NextMondayUtc().AddDays(35);
            var pendingCoach = DemandeConge.CreerCongeAnnuel(
                CoachId,
                ManagerId,
                pendingCoachStart,
                pendingCoachStart.AddDays(3),
                soldeCoach,
                coach,
                "Formation qualité NPS (démo)");
            await db.DemandeConges.AddAsync(pendingCoach, ct);
        }

        await db.SaveChangesAsync(ct);

        var validatedStart = NextMondayUtc().AddDays(14);
        var validatedEnd = validatedStart.AddDays(2);
        var validated = DemandeConge.CreerCongeAnnuel(
            EmployeeId,
            ManagerId,
            validatedStart,
            validatedEnd,
            soldeEmployee,
            employe,
            "Congé validé — aligné planning (démo)");
        validated.Valider(ManagerId, "Validé pour démo Docker.");
        soldeEmployee.DeduireSolde(validated.NombreJours);
        await db.DemandeConges.AddAsync(validated, ct);

        if (await db.EmployeSnapshots.AnyAsync(e => e.EmployeId == RpId, ct))
        {
            var rp = await db.EmployeSnapshots.FirstAsync(e => e.EmployeId == RpId, ct);
            var soldeRp = await db.SoldeConges.FirstOrDefaultAsync(s => s.EmployeId == RpId && s.Annee == annee, ct);
            if (soldeRp is null)
            {
                soldeRp = SoldeConge.Initialiser(RpId, 18, annee);
                await db.SoldeConges.AddAsync(soldeRp, ct);
            }

            var validatedRpStart = NextMondayUtc().AddDays(42);
            var validatedRp = DemandeConge.CreerCongeAnnuel(
                RpId,
                ManagerId,
                validatedRpStart,
                validatedRpStart.AddDays(1),
                soldeRp,
                rp,
                "Suivi projet inbound (démo)");
            validatedRp.Valider(ManagerId, "OK démo.");
            soldeRp.DeduireSolde(validatedRp.NombreJours);
            await db.DemandeConges.AddAsync(validatedRp, ct);
        }

        if (await db.EmployeSnapshots.AnyAsync(e => e.EmployeId == CoachId, ct))
        {
            var coach = await db.EmployeSnapshots.FirstAsync(e => e.EmployeId == CoachId, ct);
            var soldeCoach = await db.SoldeConges.FirstAsync(s => s.EmployeId == CoachId && s.Annee == annee, ct);
            var refusedStart = NextMondayUtc().AddDays(49);
            var refused = DemandeConge.CreerCongeAnnuel(
                CoachId,
                ManagerId,
                refusedStart,
                refusedStart.AddDays(4),
                soldeCoach,
                coach,
                "Demande refusée (démo)");
            refused.Refuser(ManagerId, "Période de forte affluence inbound — report demandé (démo).");
            await db.DemandeConges.AddAsync(refused, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedHistoriqueViaSqlAsync(CongeDbContext db, CancellationToken ct)
    {
        var annee = DateTime.UtcNow.Year;
        var histStart1 = new DateTime(annee - 1, 8, 5, 0, 0, 0, DateTimeKind.Utc);
        var histEnd1 = new DateTime(annee - 1, 8, 12, 0, 0, 0, DateTimeKind.Utc);
        var histStart2 = new DateTime(annee, 1, 6, 0, 0, 0, DateTimeKind.Utc);
        var histEnd2 = new DateTime(annee, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var histStart3 = new DateTime(annee, 3, 17, 0, 0, 0, DateTimeKind.Utc);
        var histEnd3 = new DateTime(annee, 3, 19, 0, 0, 0, DateTimeKind.Utc);

        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO demandes_conge ("Id", "EmployeId", "ManagerId", "TypeConge", "DateDebut", "DateFin", "NombreJours", "Statut", "Motif", "DateDemande", "DateDecision")
            SELECT gen_random_uuid(), '11111111-1111-4111-8111-111111111103'::uuid, '11111111-1111-4111-8111-111111111105'::uuid,
                   'Annuel', {0}, {1}, 6, 'Validee', 'Congé août 2025 (démo)', {0}, {1}
            WHERE NOT EXISTS (
                SELECT 1 FROM demandes_conge WHERE "EmployeId" = '11111111-1111-4111-8111-111111111103'::uuid
                  AND "Statut" = 'Validee' AND "DateDebut" = {0});

            INSERT INTO demandes_conge ("Id", "EmployeId", "ManagerId", "TypeConge", "DateDebut", "DateFin", "NombreJours", "Statut", "Motif", "DateDemande", "DateDecision")
            SELECT gen_random_uuid(), '11111111-1111-4111-8111-111111111101'::uuid, '11111111-1111-4111-8111-111111111105'::uuid,
                   'Annuel', {2}, {3}, 4, 'Validee', 'Congé janvier 2026 (démo)', {2}, {3}
            WHERE NOT EXISTS (
                SELECT 1 FROM demandes_conge WHERE "EmployeId" = '11111111-1111-4111-8111-111111111101'::uuid
                  AND "DateDebut" = {2});

            INSERT INTO demandes_conge ("Id", "EmployeId", "ManagerId", "TypeConge", "DateDebut", "DateFin", "NombreJours", "Statut", "Motif", "CommentaireManager", "DateDemande", "DateDecision")
            SELECT gen_random_uuid(), '11111111-1111-4111-8111-111111111106'::uuid, '11111111-1111-4111-8111-111111111105'::uuid,
                   'Annuel', {4}, {5}, 3, 'Refusee', 'Demande mars (démo)', 'Charge inbound élevée (démo)', {4}, {5}
            WHERE NOT EXISTS (
                SELECT 1 FROM demandes_conge WHERE "EmployeId" = '11111111-1111-4111-8111-111111111106'::uuid
                  AND "Statut" = 'Refusee' AND "DateDebut" = {4});

            INSERT INTO demandes_conge ("Id", "EmployeId", "ManagerId", "TypeConge", "DateDebut", "DateFin", "NombreJours", "Statut", "Motif", "DateDemande")
            SELECT gen_random_uuid(), '11111111-1111-4111-8111-111111111111'::uuid, '11111111-1111-4111-8111-111111111105'::uuid,
                   'Annuel', {6}, {7}, 2, 'Validee', 'Historique superviseur (démo)', {6}
            WHERE NOT EXISTS (
                SELECT 1 FROM demandes_conge WHERE "EmployeId" = '11111111-1111-4111-8111-111111111111'::uuid
                  AND "DateDebut" = {6});
            """,
            [histStart1, histEnd1, histStart2, histEnd2, histStart3, histEnd3,
                new DateTime(annee, 2, 3, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(annee, 2, 4, 0, 0, 0, DateTimeKind.Utc)],
            ct);
    }

    private static DateTime NextMondayUtc()
    {
        var today = DateTime.UtcNow.Date;
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0)
            daysUntilMonday = 7;
        return today.AddDays(daysUntilMonday);
    }
}
