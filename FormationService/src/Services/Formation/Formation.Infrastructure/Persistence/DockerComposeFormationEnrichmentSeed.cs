using Formation.Domain.Entities;
using Formation.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Formation.Infrastructure.Persistence;

/// <summary>
/// Catalogue formations + TrainingSessions + annuaire contact centre pour Docker.
/// </summary>
public static class DockerComposeFormationEnrichmentSeed
{
    private const string LegacyMarkerTitre = "Accueil nouveaux agents inbound";
    private const string SessionMarkerTitle = "Qualité softphone — Agents 1er niveau (contact centre)";

    public static async Task ApplyIfEnabledAsync(
        IConfiguration configuration,
        FormationDbContext db,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        if (!IsEnabled(configuration))
            return;

        await EnsureAnnuaireAsync(db, logger, ct);
        await EnsureLegacyCatalogueAsync(db, logger, ct);
        await EnsureTrainingSessionsAsync(db, logger, ct);
        await EnsurePilotageAnnuaireAndSessionsAsync(db, logger, ct);
        await EnsureInitialTrainingPathsAsync(db, logger, ct);
        await EnsureFormateurAnimatedSessionsAsync(db, logger, ct);
    }

    private static bool IsEnabled(IConfiguration configuration) =>
        string.Equals(configuration["KYNTUS_FORMATION_DEMO_SEED"], "true", StringComparison.OrdinalIgnoreCase)
        && string.Equals(configuration["KYNTUS_DEMO_ENRICHMENT"] ?? "true", "true", StringComparison.OrdinalIgnoreCase);

    private static async Task EnsureAnnuaireAsync(FormationDbContext db, ILogger? logger, CancellationToken ct)
    {
        var manager = ContactCentreRoster.ByPrimeId("e9")!;
        var added = 0;
        foreach (var emp in ContactCentreRoster.Employees)
        {
            if (await db.EmployeAnnuaires.AnyAsync(a => a.EmployeId == emp.Guid, ct))
                continue;

            db.EmployeAnnuaires.Add(new EmployeAnnuaire
            {
                Id = Guid.NewGuid(),
                EmployeId = emp.Guid,
                Nom = emp.LastName,
                Prenom = emp.FirstName,
                Email = emp.ContactEmail,
                Role = emp.Role,
                ManagerId = emp.PrimeId == "e9" ? Guid.Empty : manager.Guid,
                DerniereModification = DateTime.UtcNow,
            });
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(ct);
            logger?.LogInformation("Formation annuaire : {Count} collaborateurs contact centre.", added);
        }
    }

    private static async Task EnsureLegacyCatalogueAsync(FormationDbContext db, ILogger? logger, CancellationToken ct)
    {
        if (await db.Formations.AnyAsync(f => EF.Functions.Like(f.Titre, $"%{LegacyMarkerTitre}%"), ct))
        {
            logger?.LogInformation("Formation catalogue legacy déjà présent.");
            return;
        }

        var roster = ContactCentreRoster.Employees
            .Where(e => e.PrimeId != "e-admin" && e.PrimeId != "e5")
            .Select(e => (e.Guid, ContactCentreRoster.DisplayName(e)))
            .ToArray();

        var now = DateTime.UtcNow;
        var specs = new (string Titre, string Desc, string Formateur, DateTime Debut, DateTime Fin, int Cap, (Guid, string)[]? Inscrits)[]
        {
            ("Accueil nouveaux agents inbound — grands comptes",
                "Parcours d'intégration voice/chat pour la plateforme inbound Casablanca.",
                "Latifa Mansouri", now.AddDays(5), now.AddDays(7), 25, roster.Take(8).ToArray()),
            ("Qualité & NPS — rappels satisfaction",
                "Techniques de rappel et scripts NPS pour enquêtes satisfaction.",
                "Omar Tazi", now.AddDays(12), now.AddDays(13), 20, roster.Take(5).ToArray()),
            ("Procédures réclamations & rétention",
                "Workflow réclamations et offres de rétention — cellule Casablanca.",
                "Kenza Alami", now.AddDays(18), now.AddDays(19), 15, roster.Take(3).ToArray()),
            ("Soft skills — relation client marocaine",
                "Communication et gestion de conflit — contexte marocain.",
                "Hicham Benjelloun", now.AddDays(25), now.AddDays(26), 30, null),
            ("Supervision connectivité & ACD (historique)",
                "Session passée — supervision télécom.",
                "Nadia Benchrif", now.AddDays(-30), now.AddDays(-28), 12, null),
            ("Formation rétention — historique",
                "Session passée — techniques de rétention.",
                "Ghita Benkirane", now.AddDays(-14), now.AddDays(-13), 10, null),
        };

        var created = 0;
        foreach (var spec in specs)
        {
            var formation = CreateValidated(spec.Titre, spec.Desc, spec.Formateur, spec.Debut, spec.Fin, spec.Cap, 0);
            db.Formations.Add(formation);
            await db.SaveChangesAsync(ct);

            if (spec.Inscrits is { Length: > 0 })
            {
                foreach (var (employeId, nom) in spec.Inscrits)
                    formation.Inscrire(employeId, nom);
                await db.SaveChangesAsync(ct);
            }

            created++;
        }

        logger?.LogInformation("Formation catalogue legacy : {Count} formations créées.", created);
    }

    private static async Task EnsureTrainingSessionsAsync(FormationDbContext db, ILogger? logger, CancellationToken ct)
    {
        if (await db.TrainingSessions.AnyAsync(s => s.Title == SessionMarkerTitle, ct))
        {
            logger?.LogInformation("Formation TrainingSessions déjà présentes.");
            return;
        }

        var e1 = ContactCentreRoster.ByPrimeId("e1")!;
        var e2 = ContactCentreRoster.ByPrimeId("e2")!;
        var e4 = ContactCentreRoster.ByPrimeId("e4")!;
        var e6 = ContactCentreRoster.ByPrimeId("e6")!;
        var e8 = ContactCentreRoster.ByPrimeId("e8")!;
        var e9 = ContactCentreRoster.ByPrimeId("e9")!;
        var e10 = ContactCentreRoster.ByPrimeId("e10")!;
        var e5 = ContactCentreRoster.ByPrimeId("e5")!;
        var now = DateTime.UtcNow;

        var sessions = new List<(TrainingSession Session, (ContactCentreRoster.Employee Emp, TrainingAssignmentStatus Status)[] Assignments)>
        {
            (
                new TrainingSession
                {
                    Id = Guid.NewGuid(),
                    Title = SessionMarkerTitle,
                    Description = "Bonnes pratiques softphone et qualité audio — Agents 1er niveau.",
                    Type = TrainingSessionType.Continue,
                    AnimatorKind = AnimatorKind.Internal,
                    AnimatorUserId = e8.Guid,
                    PlannedStart = now.AddDays(3).Date.AddHours(9),
                    PlannedEnd = now.AddDays(3).Date.AddHours(12),
                    Capacity = 12,
                    Status = TrainingSessionStatus.Scheduled,
                    CreatedByUserId = e6.Guid.ToString(),
                    CreatedAt = now,
                    UpdatedAt = now,
                },
                new[]
                {
                    (e1, TrainingAssignmentStatus.Assigned),
                    (e2, TrainingAssignmentStatus.Assigned),
                    (e4, TrainingAssignmentStatus.Assigned),
                    (e10, TrainingAssignmentStatus.Assigned),
                }
            ),
            (
                new TrainingSession
                {
                    Id = Guid.NewGuid(),
                    Title = "Parcours rétention — scripts & offres",
                    Description = "Atelier cellules réclamations & rétention.",
                    Type = TrainingSessionType.Continue,
                    AnimatorKind = AnimatorKind.Internal,
                    AnimatorUserId = e5.Guid,
                    PlannedStart = now.AddDays(-1).Date.AddHours(10),
                    PlannedEnd = now.AddDays(-1).Date.AddHours(16),
                    Capacity = 10,
                    Status = TrainingSessionStatus.InProgress,
                    CreatedByUserId = e6.Guid.ToString(),
                    CreatedAt = now.AddDays(-5),
                    UpdatedAt = now,
                },
                new[]
                {
                    (e1, TrainingAssignmentStatus.InProgress),
                    (e2, TrainingAssignmentStatus.Completed),
                    (e4, TrainingAssignmentStatus.Failed),
                    (e9, TrainingAssignmentStatus.Assigned),
                }
            ),
            (
                new TrainingSession
                {
                    Id = Guid.NewGuid(),
                    Title = "NPS & enquêtes satisfaction — rappel clients",
                    Description = "Session passée — techniques de rappel NPS.",
                    Type = TrainingSessionType.Continue,
                    AnimatorKind = AnimatorKind.Internal,
                    AnimatorUserId = e6.Guid,
                    PlannedStart = now.AddDays(-14).Date.AddHours(9),
                    PlannedEnd = now.AddDays(-14).Date.AddHours(17),
                    Capacity = 15,
                    Status = TrainingSessionStatus.Completed,
                    CreatedByUserId = e5.Guid.ToString(),
                    CreatedAt = now.AddDays(-20),
                    UpdatedAt = now.AddDays(-14),
                },
                new[]
                {
                    (e1, TrainingAssignmentStatus.Completed),
                    (e2, TrainingAssignmentStatus.Completed),
                    (e4, TrainingAssignmentStatus.Failed),
                    (e8, TrainingAssignmentStatus.Completed),
                    (e10, TrainingAssignmentStatus.Completed),
                }
            ),
            (
                new TrainingSession
                {
                    Id = Guid.NewGuid(),
                    Title = "Onboarding coach qualité — brouillon",
                    Description = "Brouillon RH formation — non publié.",
                    Type = TrainingSessionType.Continue,
                    AnimatorKind = AnimatorKind.Internal,
                    AnimatorUserId = e6.Guid,
                    PlannedStart = now.AddDays(20).Date.AddHours(9),
                    PlannedEnd = now.AddDays(21).Date.AddHours(17),
                    Capacity = 8,
                    Status = TrainingSessionStatus.Draft,
                    CreatedByUserId = e6.Guid.ToString(),
                    CreatedAt = now,
                    UpdatedAt = now,
                },
                Array.Empty<(ContactCentreRoster.Employee, TrainingAssignmentStatus)>()
            ),
            (
                new TrainingSession
                {
                    Id = Guid.NewGuid(),
                    Title = "Supervision ACD — connectivité réseau",
                    Description = "Session planifiée pour le pôle Support SI.",
                    Type = TrainingSessionType.Continue,
                    AnimatorKind = AnimatorKind.Internal,
                    AnimatorUserId = e9.Guid,
                    PlannedStart = now.AddDays(10).Date.AddHours(14),
                    PlannedEnd = now.AddDays(10).Date.AddHours(17),
                    Capacity = 6,
                    Status = TrainingSessionStatus.Scheduled,
                    CreatedByUserId = e6.Guid.ToString(),
                    CreatedAt = now,
                    UpdatedAt = now,
                },
                new[]
                {
                    (e1, TrainingAssignmentStatus.Assigned),
                    (e8, TrainingAssignmentStatus.Assigned),
                }
            ),
        };

        foreach (var (session, assignments) in sessions)
        {
            db.TrainingSessions.Add(session);
            foreach (var (emp, status) in assignments)
            {
                db.TrainingAssignments.Add(new TrainingAssignment
                {
                    Id = Guid.NewGuid(),
                    SessionId = session.Id,
                    EmployeeId = emp.Guid,
                    EmployeeName = ContactCentreRoster.DisplayName(emp),
                    Status = status,
                    AssignedAt = now.AddDays(-2),
                    UpdatedAt = now,
                });
            }
        }

        await db.SaveChangesAsync(ct);
        logger?.LogInformation("Formation TrainingSessions : {Count} sessions créées.", sessions.Count);
    }

    private const string PilotageSessionMarker = "Indicateurs KPI — cellule suivi KPI (pilotage performance)";

    private static async Task EnsurePilotageAnnuaireAndSessionsAsync(
        FormationDbContext db,
        ILogger? logger,
        CancellationToken ct)
    {
        var salim = PilotagePerformanceRoster.Require("Ouazzani");
        var added = 0;
        foreach (var emp in PilotagePerformanceRoster.Employees)
        {
            if (await db.EmployeAnnuaires.AnyAsync(a => a.EmployeId == emp.Guid, ct))
                continue;
            db.EmployeAnnuaires.Add(new EmployeAnnuaire
            {
                Id = Guid.NewGuid(),
                EmployeId = emp.Guid,
                Nom = emp.LastName,
                Prenom = emp.FirstName,
                Email = emp.Email,
                Role = emp.Role,
                ManagerId = emp.Guid == salim.Guid ? Guid.Empty : salim.Guid,
                DerniereModification = DateTime.UtcNow,
            });
            added++;
        }

        if (added > 0)
            await db.SaveChangesAsync(ct);

        if (await db.TrainingSessions.AnyAsync(s => s.Title == PilotageSessionMarker, ct))
        {
            logger?.LogInformation("Formation pilotage : sessions déjà présentes.");
            return;
        }

        var malak = PilotagePerformanceRoster.Require("Souiri");
        var younes = PilotagePerformanceRoster.Require("Elidrissi");
        var chaima = PilotagePerformanceRoster.Require("Benali");
        var hamid = PilotagePerformanceRoster.Require("Fellah");
        var othmane = PilotagePerformanceRoster.Require("Kabbaj");
        var asmae = PilotagePerformanceRoster.Require("Tazi");
        var rania = PilotagePerformanceRoster.Require("Karimi");
        var now = DateTime.UtcNow;

        var sessions = new List<(TrainingSession Session, (PilotagePerformanceRoster.Employee Emp, TrainingAssignmentStatus Status)[] Assignments)>
        {
            (
                new TrainingSession
                {
                    Id = Guid.NewGuid(),
                    Title = PilotageSessionMarker,
                    Description = "Lecture et animation des tableaux de bord KPI — service analyse opérationnelle.",
                    Type = TrainingSessionType.Continue,
                    AnimatorKind = AnimatorKind.Internal,
                    AnimatorUserId = younes.Guid,
                    PlannedStart = now.AddDays(2).Date.AddHours(9),
                    PlannedEnd = now.AddDays(2).Date.AddHours(12),
                    Capacity = 10,
                    Status = TrainingSessionStatus.Scheduled,
                    CreatedByUserId = malak.Guid.ToString(),
                    CreatedAt = now,
                    UpdatedAt = now,
                },
                new[]
                {
                    (chaima, TrainingAssignmentStatus.Assigned),
                    (hamid, TrainingAssignmentStatus.Assigned),
                    (othmane, TrainingAssignmentStatus.Assigned),
                    (asmae, TrainingAssignmentStatus.Assigned),
                    (rania, TrainingAssignmentStatus.Assigned),
                }
            ),
            (
                new TrainingSession
                {
                    Id = Guid.NewGuid(),
                    Title = "Analyse opérationnelle — reporting hebdo",
                    Description = "Atelier en cours pour les pilotes du service analyse opérationnelle.",
                    Type = TrainingSessionType.Continue,
                    AnimatorKind = AnimatorKind.Internal,
                    AnimatorUserId = salim.Guid,
                    PlannedStart = now.AddDays(-1).Date.AddHours(10),
                    PlannedEnd = now.AddDays(-1).Date.AddHours(16),
                    Capacity = 8,
                    Status = TrainingSessionStatus.InProgress,
                    CreatedByUserId = malak.Guid.ToString(),
                    CreatedAt = now.AddDays(-4),
                    UpdatedAt = now,
                },
                new[]
                {
                    (chaima, TrainingAssignmentStatus.InProgress),
                    (hamid, TrainingAssignmentStatus.Completed),
                    (othmane, TrainingAssignmentStatus.Failed),
                    (younes, TrainingAssignmentStatus.Assigned),
                }
            ),
            (
                new TrainingSession
                {
                    Id = Guid.NewGuid(),
                    Title = "Pilotage performance — rituels management",
                    Description = "Session passée — rituels chef de projet / superviseur.",
                    Type = TrainingSessionType.Continue,
                    AnimatorKind = AnimatorKind.Internal,
                    AnimatorUserId = malak.Guid,
                    PlannedStart = now.AddDays(-12).Date.AddHours(9),
                    PlannedEnd = now.AddDays(-12).Date.AddHours(17),
                    Capacity = 6,
                    Status = TrainingSessionStatus.Completed,
                    CreatedByUserId = malak.Guid.ToString(),
                    CreatedAt = now.AddDays(-18),
                    UpdatedAt = now.AddDays(-12),
                },
                new[]
                {
                    (salim, TrainingAssignmentStatus.Completed),
                    (younes, TrainingAssignmentStatus.Completed),
                    (chaima, TrainingAssignmentStatus.Completed),
                    (hamid, TrainingAssignmentStatus.Failed),
                }
            ),
        };

        foreach (var (session, assignments) in sessions)
        {
            db.TrainingSessions.Add(session);
            foreach (var (emp, status) in assignments)
            {
                db.TrainingAssignments.Add(new TrainingAssignment
                {
                    Id = Guid.NewGuid(),
                    SessionId = session.Id,
                    EmployeeId = emp.Guid,
                    EmployeeName = PilotagePerformanceRoster.DisplayName(emp),
                    Status = status,
                    AssignedAt = now.AddDays(-3),
                    UpdatedAt = now,
                });
            }
        }

        // Catalogue legacy : 2 formations pour le pôle
        if (!await db.Formations.AnyAsync(f => EF.Functions.Like(f.Titre, "%pilotage performance%"), ct))
        {
            var f1 = CreateValidated(
                "Acculturation pilotage performance",
                "Parcours d'intégration KPI / reporting pour le pôle pilotage performance.",
                "Malak Souiri",
                now.AddDays(6),
                now.AddDays(7),
                15,
                0);
            db.Formations.Add(f1);
            await db.SaveChangesAsync(ct);
            foreach (var emp in new[] { chaima, hamid, othmane, asmae, rania })
                f1.Inscrire(emp.Guid, PilotagePerformanceRoster.DisplayName(emp));
            await db.SaveChangesAsync(ct);
        }

        await db.SaveChangesAsync(ct);
        logger?.LogInformation("Formation pilotage : {Count} sessions + annuaire.", sessions.Count);
    }

    /// <summary>SubjectId Auth formation@kyntus.ma / e6.</summary>
    private static readonly Guid FormateurKyntusId = Guid.Parse("11111111-1111-4111-8111-111111111110");

    /// <summary>SubjectId Auth formateur@gmail.com.</summary>
    private static readonly Guid FormateurGmailId = Guid.Parse("11111111-1111-4111-8111-111111111120");

    private const string InitialPathMarkerName = "Sara Bennani (parcours initiale démo)";
    private const string FormateurSessionMarker = "Atelier formateur — rituels d'accueil (Mes sessions)";

    private static async Task EnsureInitialTrainingPathsAsync(
        FormationDbContext db,
        ILogger? logger,
        CancellationToken ct)
    {
        if (await db.InitialTrainingPaths.AnyAsync(p => p.EmployeeName == InitialPathMarkerName, ct))
        {
            logger?.LogInformation("Formation initiale : parcours déjà seedés.");
            return;
        }

        var now = DateTime.UtcNow;
        var e1 = ContactCentreRoster.ByPrimeId("e1")!;
        var e2 = ContactCentreRoster.ByPrimeId("e2")!;
        var e4 = ContactCentreRoster.ByPrimeId("e4")!;
        var chaima = PilotagePerformanceRoster.Require("Benali");
        var hamid = PilotagePerformanceRoster.Require("Fellah");
        var othmane = PilotagePerformanceRoster.Require("Kabbaj");
        var asmae = PilotagePerformanceRoster.Require("Tazi");
        var rania = PilotagePerformanceRoster.Require("Karimi");

        var paths = new List<InitialTrainingPath>
        {
            new()
            {
                Id = Guid.NewGuid(),
                EmployeeId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa01"),
                EmployeeName = InitialPathMarkerName,
                DateDebut = now.AddDays(-5),
                DateFinPrevue = now.AddDays(10),
                Status = InitialTrainingStatus.EnCours,
                CreatedAt = now.AddDays(-5),
                UpdatedAt = now,
            },
            new()
            {
                Id = Guid.NewGuid(),
                EmployeeId = e2.Guid,
                EmployeeName = ContactCentreRoster.DisplayName(e2),
                DateDebut = now.AddDays(-12),
                DateFinPrevue = now.AddDays(3),
                Status = InitialTrainingStatus.QuizASaisir,
                QuizScore = 55,
                QuizPassed = false,
                QuizRecordedBy = "Formateur",
                FormateurComment = "À repasser — seuil 70 % non atteint",
                CreatedAt = now.AddDays(-12),
                UpdatedAt = now.AddDays(-1),
            },
            new()
            {
                Id = Guid.NewGuid(),
                EmployeeId = e4.Guid,
                EmployeeName = ContactCentreRoster.DisplayName(e4),
                DateDebut = now.AddDays(-20),
                DateFinPrevue = now.AddDays(2),
                Status = InitialTrainingStatus.AttenteValidationFormateur,
                QuizScore = 82,
                QuizPassed = true,
                QuizRecordedBy = "Formateur",
                FormateurComment = "Bon niveau — validation formateur attendue",
                CreatedAt = now.AddDays(-20),
                UpdatedAt = now.AddHours(-6),
            },
            new()
            {
                Id = Guid.NewGuid(),
                EmployeeId = chaima.Guid,
                EmployeeName = PilotagePerformanceRoster.DisplayName(chaima),
                DateDebut = now.AddDays(-18),
                DateFinPrevue = now.AddDays(5),
                Status = InitialTrainingStatus.AttenteValidationRh,
                QuizScore = 91,
                QuizPassed = true,
                QuizRecordedBy = "Formateur",
                FormateurComment = "Validé formateur — en attente RH",
                FormateurValidatedAt = now.AddDays(-2),
                CreatedAt = now.AddDays(-18),
                UpdatedAt = now.AddDays(-2),
            },
            new()
            {
                Id = Guid.NewGuid(),
                EmployeeId = hamid.Guid,
                EmployeeName = PilotagePerformanceRoster.DisplayName(hamid),
                DateDebut = now.AddDays(-8),
                DateFinPrevue = now.AddDays(14),
                Status = InitialTrainingStatus.EnCours,
                CreatedAt = now.AddDays(-8),
                UpdatedAt = now,
            },
            new()
            {
                Id = Guid.NewGuid(),
                EmployeeId = othmane.Guid,
                EmployeeName = PilotagePerformanceRoster.DisplayName(othmane),
                DateDebut = now.AddDays(-3),
                DateFinPrevue = now.AddDays(18),
                Status = InitialTrainingStatus.EnCours,
                CreatedAt = now.AddDays(-3),
                UpdatedAt = now,
            },
            new()
            {
                Id = Guid.NewGuid(),
                EmployeeId = asmae.Guid,
                EmployeeName = PilotagePerformanceRoster.DisplayName(asmae),
                DateDebut = now.AddDays(-25),
                DateFinPrevue = now.AddDays(-2),
                Status = InitialTrainingStatus.AttenteValidationFormateur,
                QuizScore = 74,
                QuizPassed = true,
                QuizRecordedBy = "Formateur",
                CreatedAt = now.AddDays(-25),
                UpdatedAt = now.AddDays(-1),
            },
            new()
            {
                Id = Guid.NewGuid(),
                EmployeeId = rania.Guid,
                EmployeeName = PilotagePerformanceRoster.DisplayName(rania),
                DateDebut = now.AddDays(-40),
                DateFinPrevue = now.AddDays(-10),
                Status = InitialTrainingStatus.EnProduction,
                QuizScore = 88,
                QuizPassed = true,
                QuizRecordedBy = "Formateur",
                FormateurValidatedAt = now.AddDays(-15),
                RhValidatedAt = now.AddDays(-12),
                CreatedAt = now.AddDays(-40),
                UpdatedAt = now.AddDays(-12),
            },
            new()
            {
                Id = Guid.NewGuid(),
                EmployeeId = e1.Guid,
                EmployeeName = ContactCentreRoster.DisplayName(e1),
                DateDebut = now.AddDays(-60),
                DateFinPrevue = now.AddDays(-30),
                Status = InitialTrainingStatus.Rejete,
                QuizScore = 40,
                QuizPassed = false,
                QuizRecordedBy = "Formateur",
                RejectedBy = "Formateur",
                RejectReason = "Échecs répétés au quiz — reprise prévue",
                CreatedAt = now.AddDays(-60),
                UpdatedAt = now.AddDays(-35),
            },
        };

        db.InitialTrainingPaths.AddRange(paths);
        await db.SaveChangesAsync(ct);
        logger?.LogInformation("Formation initiale : {Count} parcours créés (file formateur + admin).", paths.Count);
    }

    private static async Task EnsureFormateurAnimatedSessionsAsync(
        FormationDbContext db,
        ILogger? logger,
        CancellationToken ct)
    {
        if (await db.TrainingSessions.AnyAsync(s => s.Title == FormateurSessionMarker, ct))
        {
            logger?.LogInformation("Formation Mes sessions formateur : déjà seedées.");
            return;
        }

        var now = DateTime.UtcNow;
        var assignees = new[]
        {
            ContactCentreRoster.ByPrimeId("e1")!,
            ContactCentreRoster.ByPrimeId("e2")!,
            ContactCentreRoster.ByPrimeId("e4")!,
        };
        var pilotAssignees = new[]
        {
            PilotagePerformanceRoster.Require("Benali"),
            PilotagePerformanceRoster.Require("Fellah"),
            PilotagePerformanceRoster.Require("Kabbaj"),
        };

        var animatorIds = new[] { FormateurKyntusId, FormateurGmailId };
        var created = 0;

        foreach (var animatorId in animatorIds)
        {
            var sessions = new (string Title, string Desc, DateTime Start, DateTime End, TrainingSessionStatus Status, bool AssignPilots)[]
            {
                (
                    animatorId == FormateurGmailId ? FormateurSessionMarker : "Atelier formateur — onboarding inbound",
                    "Session animée par l'équipe formation — rituels d'accueil et quiz.",
                    now.AddDays(2).Date.AddHours(9),
                    now.AddDays(2).Date.AddHours(12),
                    TrainingSessionStatus.Scheduled,
                    false
                ),
                (
                    animatorId == FormateurGmailId
                        ? "Coaching qualité — file formateur (en cours)"
                        : "Coaching qualité — équipe formation (en cours)",
                    "Animation live — suivi des nouveaux arrivants.",
                    now.AddDays(-1).Date.AddHours(10),
                    now.AddDays(-1).Date.AddHours(16),
                    TrainingSessionStatus.InProgress,
                    true
                ),
                (
                    animatorId == FormateurGmailId
                        ? "Bilan formation initiale — historique formateur"
                        : "Bilan formation initiale — historique équipe formation",
                    "Session terminée — bilan parcours initiale.",
                    now.AddDays(-10).Date.AddHours(9),
                    now.AddDays(-10).Date.AddHours(17),
                    TrainingSessionStatus.Completed,
                    true
                ),
            };

            foreach (var spec in sessions)
            {
                if (await db.TrainingSessions.AnyAsync(
                        s => s.Title == spec.Title && s.AnimatorUserId == animatorId, ct))
                    continue;

                var session = new TrainingSession
                {
                    Id = Guid.NewGuid(),
                    Title = spec.Title,
                    Description = spec.Desc,
                    Type = TrainingSessionType.Continue,
                    AnimatorKind = AnimatorKind.Internal,
                    AnimatorUserId = animatorId,
                    PlannedStart = spec.Start,
                    PlannedEnd = spec.End,
                    Capacity = 12,
                    Status = spec.Status,
                    CreatedByUserId = animatorId.ToString(),
                    CreatedAt = now.AddDays(-5),
                    UpdatedAt = now,
                };
                db.TrainingSessions.Add(session);

                IEnumerable<(Guid Id, string Name)> people = assignees
                    .Select(a => (a.Guid, ContactCentreRoster.DisplayName(a)));
                if (spec.AssignPilots)
                {
                    people = people.Concat(
                        pilotAssignees.Select(p => (p.Guid, PilotagePerformanceRoster.DisplayName(p))));
                }

                foreach (var (empId, empName) in people)
                {
                    var status = spec.Status switch
                    {
                        TrainingSessionStatus.Completed => TrainingAssignmentStatus.Completed,
                        TrainingSessionStatus.InProgress => TrainingAssignmentStatus.InProgress,
                        _ => TrainingAssignmentStatus.Assigned,
                    };
                    db.TrainingAssignments.Add(new TrainingAssignment
                    {
                        Id = Guid.NewGuid(),
                        SessionId = session.Id,
                        EmployeeId = empId,
                        EmployeeName = empName,
                        Status = status,
                        AssignedAt = now.AddDays(-4),
                        UpdatedAt = now,
                    });
                }

                created++;
            }
        }

        await db.SaveChangesAsync(ct);
        logger?.LogInformation("Formation Mes sessions formateur : {Count} sessions (kyntus + gmail).", created);
    }

    private static FormationEntity CreateValidated(
        string titre,
        string description,
        string formateur,
        DateTime debut,
        DateTime fin,
        int capacite,
        decimal prix)
    {
        var f = FormationEntity.Create(titre, description, formateur, debut, fin, capacite, prix);
        f.Valider();
        return f;
    }
}
