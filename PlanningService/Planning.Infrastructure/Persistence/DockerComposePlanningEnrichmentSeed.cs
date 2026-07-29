using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Planning.Application.Abstractions;
using Planning.Application.DTOs.Planning;
using Planning.Domain.Entities;
using Planning.Domain.Enums;

namespace Planning.Infrastructure.Persistence;

/// <summary>
/// Événements planning (réclamations, notifications, semaines si cellule peuplée)
/// sur employés **déjà présents** — jamais de création User / org.
/// </summary>
internal static class DockerComposePlanningEnrichmentSeed
{
    private const int EnrichmentVersion = 2;
    private const string MarkerAction = "DockerPlanningEnrichmentV2";
    private const string ReclamationMarkerTitle = "Qualité audio — plateforme inbound grands comptes";

    internal static async Task ApplyIfEnabledAsync(
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken ct = default)
    {
        if (!IsEnabled(configuration))
            return;

        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var planningService = scope.ServiceProvider.GetRequiredService<IPlanningService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Planning.EnrichmentSeed");

        if (await IsAlreadyAppliedAsync(context, ct))
        {
            logger.LogInformation("Planning enrichment v{Version} déjà appliqué.", EnrichmentVersion);
            return;
        }

        // Préférer la cellule Prime c1 ; sinon la plus peuplée
        var sub = await context.SubServices
            .FirstOrDefaultAsync(s => s.PrimeServiceId == "c1" || s.Code == "c1", ct);
        if (sub is null)
        {
            var subId = await context.Users
                .Where(u => u.IsActive && u.SubServiceId != null)
                .GroupBy(u => u.SubServiceId!.Value)
                .Select(g => new { SubId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Select(x => x.SubId)
                .FirstOrDefaultAsync(ct);

            if (subId == 0)
            {
                logger.LogWarning("Planning enrichment : aucune cellule peuplée — réclamations seules si auteurs présents.");
                await SeedReclamationsAsync(context, await ResolveAnyActiveUsersAsync(context, ct), ct);
                await MarkAppliedWithoutWeekAsync(context, ct);
                return;
            }

            sub = await context.SubServices.FirstAsync(s => s.Id == subId, ct);
        }

        var manager = await context.Users
            .Include(u => u.Role)
            .Where(u => u.IsActive && u.Role.Name == "Superviseur")
            .OrderBy(u => u.Id)
            .FirstOrDefaultAsync(ct)
            ?? await context.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.Id)
                .FirstOrDefaultAsync(ct);

        if (manager is null)
        {
            logger.LogWarning("Planning enrichment ignoré : aucun utilisateur actif.");
            return;
        }

        var cellUsers = await context.Users
            .Where(u => u.SubServiceId == sub.Id && u.IsActive)
            .OrderBy(u => u.Id)
            .ToListAsync(ct);

        if (cellUsers.Count == 0)
        {
            logger.LogWarning("Planning enrichment : cellule vide — réclamations sur utilisateurs actifs globaux.");
            cellUsers = await ResolveAnyActiveUsersAsync(context, ct);
        }

        if (cellUsers.Count == 0)
        {
            logger.LogWarning("Planning enrichment ignoré : aucun employé actif.");
            return;
        }

        // Semaines : uniquement si absentes (événements sur org existante).
        var existingWeeks = await context.WeeklyPlannings.CountAsync(p => p.SubServiceId == sub.Id, ct);
        if (existingWeeks == 0)
        {
            await EnsureSaturdayGroupsAsync(context, cellUsers, manager.Id, ct);
            await SeedPlanningCongesAsync(context, cellUsers, ct);

            await planningService.SaveShiftTemplateAsync(new SaveShiftConfigDto
            {
                SubServiceId = sub.Id,
                Shifts = BuildShiftConfigItems(cellUsers.Count),
            });

            var weeks = new[] { -3, -2, -1, 0 };
            foreach (var offset in weeks)
            {
                var (weekCode, weekStart) = GetIsoWeek(offset);
                if (await context.WeeklyPlannings.AnyAsync(
                        p => p.SubServiceId == sub.Id && p.WeekCode == weekCode, ct))
                    continue;

                var created = await planningService.CreatePlanningAsync(new CreateWeeklyPlanningDto
                {
                    SubServiceId = sub.Id,
                    WeekCode = weekCode,
                    WeekStartDate = weekStart,
                    TotalEffectif = cellUsers.Count,
                });

                await planningService.GeneratePlanningFromConfigAsync(new GeneratePlanningFromConfigDto
                {
                    SubServiceId = sub.Id,
                    WeekCode = weekCode,
                    WeeklyPlanningId = created.Id,
                });

                await planningService.RecordConsultationAsync(created.Id, manager.Id);
                await planningService.PublishPlanningAsync(created.Id, manager.Id);
            }
        }
        else
        {
            logger.LogInformation(
                "Planning enrichment : {Count} semaines déjà présentes — pas de re-création.",
                existingWeeks);
            await SeedChangeRequestsOnExistingWeeksAsync(context, cellUsers, sub.Id, ct);
        }

        await SeedReclamationsAsync(context, cellUsers, ct);
        await MarkAppliedAsync(context, ct);

        logger.LogInformation("Planning enrichment v{Version} terminé (event-only).", EnrichmentVersion);
    }

    private static async Task<List<User>> ResolveAnyActiveUsersAsync(AppDbContext context, CancellationToken ct) =>
        await context.Users.Where(u => u.IsActive).OrderBy(u => u.Id).Take(20).ToListAsync(ct);

    private static bool IsEnabled(IConfiguration configuration) =>
        string.Equals(configuration["KYNTUS_PLANNING_DEMO_SEED"], "true", StringComparison.OrdinalIgnoreCase)
        && string.Equals(configuration["KYNTUS_DEMO_ENRICHMENT"] ?? "false", "true", StringComparison.OrdinalIgnoreCase);

    private static async Task<bool> IsAlreadyAppliedAsync(AppDbContext context, CancellationToken ct) =>
        await context.PlanningComments.AnyAsync(c => c.Comment == MarkerAction, ct)
        || await context.Reclamations.AnyAsync(r => r.Titre == ReclamationMarkerTitle, ct);

    private static async Task SeedChangeRequestsOnExistingWeeksAsync(
        AppDbContext context,
        List<User> cellUsers,
        int subServiceId,
        CancellationToken ct)
    {
        if (await context.PlanningChangeRequests.AnyAsync(
                c => c.Reason.Contains(MarkerAction), ct))
            return;

        var week = await context.WeeklyPlannings
            .Where(p => p.SubServiceId == subServiceId)
            .OrderByDescending(p => p.WeekStartDate)
            .FirstOrDefaultAsync(ct);
        if (week is null)
            return;

        var assignment = await context.ShiftAssignments
            .Where(a => a.WeeklyPlanningId == week.Id)
            .OrderBy(a => a.Id)
            .FirstOrDefaultAsync(ct);
        if (assignment is null)
            return;

        var employee = cellUsers.FirstOrDefault(u => u.Id == assignment.UserId)
            ?? cellUsers.FirstOrDefault(u =>
                u.Email.Equals("employee@kyntus.ma", StringComparison.OrdinalIgnoreCase))
            ?? cellUsers[0];

        context.PlanningChangeRequests.Add(new PlanningChangeRequest
        {
            WeekCode = week.WeekCode,
            RequesterUserId = employee.Id,
            CurrentAssignmentId = assignment.Id,
            Status = PlanningChangeRequestStatus.Pending,
            Reason = $"Ajustement créneau matin — {MarkerAction}",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
        });
        await context.SaveChangesAsync(ct);
    }

    private static async Task EnsureSaturdayGroupsAsync(
        AppDbContext context,
        List<User> users,
        int managerId,
        CancellationToken ct)
    {
        for (var i = 0; i < users.Count; i++)
        {
            var user = users[i];
            if (await context.SaturdayGroups.AnyAsync(sg => sg.UserId == user.Id, ct))
                continue;

            context.SaturdayGroups.Add(new SaturdayGroup
            {
                UserId = user.Id,
                GroupNumber = i % 2 == 0 ? 1 : 2,
                IsNewEmployee = false,
                AssignedBy = managerId,
                AssignedAt = DateTime.UtcNow,
            });
        }

        await context.SaveChangesAsync(ct);
    }

    private static async Task SeedPlanningCongesAsync(
        AppDbContext context,
        List<User> users,
        CancellationToken ct)
    {
        var employee = users.FirstOrDefault(u =>
            u.Email.Equals("employee@kyntus.ma", StringComparison.OrdinalIgnoreCase)
            || u.Email.Equals("yasmine.elidrissi@contactcentre.ma", StringComparison.OrdinalIgnoreCase))
            ?? users.FirstOrDefault();
        if (employee is null)
            return;

        var (_, weekStart) = GetIsoWeek(2);
        var start = weekStart.AddDays(1);
        var end = start.AddDays(2);

        if (await context.Conges.AnyAsync(
                c => c.UserId == employee.Id && c.StartDate == start, ct))
            return;

        context.Conges.Add(new Conge
        {
            UserId = employee.Id,
            StartDate = start,
            EndDate = end,
            Reason = "Congé annuel — plateforme inbound",
            Status = CongeStatus.Approved,
            AbsenceType = AbsenceType.CongesPayes,
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync(ct);
    }

    private static async Task SeedReclamationsAsync(
        AppDbContext context,
        List<User> users,
        CancellationToken ct)
    {
        if (users.Count == 0)
            return;

        if (await context.Reclamations.AnyAsync(r => r.Titre == ReclamationMarkerTitle, ct))
            return;

        var author = users.FirstOrDefault(u =>
            u.Email.Equals("employee@kyntus.ma", StringComparison.OrdinalIgnoreCase)
            || u.Email.Equals("yasmine.elidrissi@contactcentre.ma", StringComparison.OrdinalIgnoreCase))
            ?? users[0];

        var second = users.Count > 1 ? users[1] : author;
        var assigneeGuid = ContactCentreRoster.Employees.FirstOrDefault(e => e.PrimeId == "e10")?.Guid.ToString()
            ?? second.Guid.ToString();
        var assigneeNom = ContactCentreRoster.Employees.FirstOrDefault(e => e.PrimeId == "e10") is { } a
            ? ContactCentreRoster.DisplayName(a)
            : $"{second.FirstName} {second.LastName}";

        var now = DateTime.UtcNow;
        var specs = new (string Titre, string Desc, ReclamationType Type, ReclamationStatus Status, Priority Prio, int DaysAgo, bool Assign)[]
        {
            (ReclamationMarkerTitle, "Coupures intermittentes sur le softphone pendant les pics d'appels.", ReclamationType.Technique, ReclamationStatus.Soumise, Priority.Haute, 2, false),
            ("Délai traitement réclamations — cellule rétention", "Demande d'accélération du circuit validation pour offres de rétention.", ReclamationType.ServiceQualite, ReclamationStatus.EnCours, Priority.Normale, 5, true),
            ("Planning samedi — supervision connectivité ACD", "Rotation groupe samedi à rééquilibrer pour la cellule Agents 1er niveau.", ReclamationType.Administrative, ReclamationStatus.Soumise, Priority.Normale, 1, false),
            ("Casque micro défectueux — poste open space", "Remplacement matériel demandé avant le shift du soir.", ReclamationType.Technique, ReclamationStatus.EnCours, Priority.Haute, 3, true),
            ("Script NPS obsolète — campagne satisfaction", "Mettre à jour le script d'enquête NPS pour la file inbound.", ReclamationType.ServiceQualite, ReclamationStatus.Soumise, Priority.Normale, 4, false),
            ("Accès CRM temporairement bloqué", "Compte verrouillé après 3 tentatives — déblocage urgent.", ReclamationType.Technique, ReclamationStatus.Cloturee, Priority.Haute, 10, true),
            ("Salle formation — disponibilité créneau", "Réservation salle pour session softphone (démo).", ReclamationType.Administrative, ReclamationStatus.Cloturee, Priority.Basse, 12, false),
            ("Écart pause déjeuner — planning publié", "Pause non alignée avec le template de la cellule.", ReclamationType.Administrative, ReclamationStatus.Soumise, Priority.Normale, 0, false),
            ("Qualité appels — bruit de fond open space", "Demande isolation acoustique postes 12–18.", ReclamationType.ServiceQualite, ReclamationStatus.EnCours, Priority.Normale, 6, true),
            ("Badge accès parking — renouvellement", "Badge expiré pour agents équipe matin.", ReclamationType.Administrative, ReclamationStatus.Cloturee, Priority.Basse, 14, false),
        };

        foreach (var s in specs)
        {
            context.Reclamations.Add(new Reclamation
            {
                Titre = s.Titre,
                Description = s.Desc,
                Type = s.Type,
                Status = s.Status,
                Priorite = s.Prio,
                AuteurId = author.Guid.ToString(),
                AuteurNom = $"{author.FirstName} {author.LastName}",
                AuteurRole = "Pilote",
                AssigneeId = s.Assign ? assigneeGuid : null,
                AssigneeNom = s.Assign ? assigneeNom : null,
                CreatedAt = now.AddDays(-s.DaysAgo),
                UpdatedAt = now.AddDays(-Math.Max(0, s.DaysAgo - 1)),
            });
        }

        await context.SaveChangesAsync(ct);
    }

    private static async Task MarkAppliedAsync(AppDbContext context, CancellationToken ct)
    {
        if (await context.PlanningComments.AnyAsync(c => c.Comment == MarkerAction, ct))
            return;

        var planning = await context.WeeklyPlannings
            .OrderByDescending(p => p.WeekStartDate)
            .FirstOrDefaultAsync(ct);
        if (planning is null)
            return;

        context.PlanningComments.Add(new PlanningComment
        {
            WeeklyPlanningId = planning.Id,
            UserId = planning.ValidatedBy ?? 1,
            CreatedBy = planning.ValidatedBy ?? 1,
            Comment = MarkerAction,
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync(ct);
    }

    private static async Task MarkAppliedWithoutWeekAsync(AppDbContext context, CancellationToken ct)
    {
        // Idempotence via titre réclamation marqueur (déjà seedé).
        await Task.CompletedTask;
    }

    private static List<ShiftConfigItemDto> BuildShiftConfigItems(int totalEffectif)
    {
        var perShift = Math.Max(1, totalEffectif / 4);
        var remainder = totalEffectif - perShift * 4;
        var counts = new[] { perShift, perShift, perShift, perShift + remainder };

        return new List<ShiftConfigItemDto>
        {
            new()
            {
                Label = "8h",
                StartTime = "08:00",
                WorkHours = 8,
                BreakDurationMinutes = 60,
                BreakRangeStart = "12:00",
                BreakRangeEnd = "13:00",
                RequiredCount = counts[0],
                DisplayOrder = 1,
            },
            new()
            {
                Label = "9h",
                StartTime = "09:00",
                WorkHours = 8,
                BreakDurationMinutes = 60,
                BreakRangeStart = "13:00",
                BreakRangeEnd = "14:00",
                RequiredCount = counts[1],
                DisplayOrder = 2,
            },
            new()
            {
                Label = "10h",
                StartTime = "10:00",
                WorkHours = 8,
                BreakDurationMinutes = 60,
                BreakRangeStart = "14:00",
                BreakRangeEnd = "15:00",
                RequiredCount = counts[2],
                DisplayOrder = 3,
            },
            new()
            {
                Label = "11h",
                StartTime = "11:00",
                WorkHours = 8,
                BreakDurationMinutes = 60,
                BreakRangeStart = "15:00",
                BreakRangeEnd = "16:00",
                RequiredCount = counts[3],
                DisplayOrder = 4,
            },
        };
    }

    private static (string WeekCode, DateOnly WeekStart) GetIsoWeek(int offsetFromCurrent)
    {
        var dt = DateTime.UtcNow.AddDays(offsetFromCurrent * 7);
        var year = ISOWeek.GetYear(dt);
        var week = ISOWeek.GetWeekOfYear(dt);
        var monday = DateOnly.FromDateTime(ISOWeek.ToDateTime(year, week, DayOfWeek.Monday));
        return ($"{year}-W{week:D2}", monday);
    }
}
