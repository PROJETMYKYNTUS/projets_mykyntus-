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
/// Plannings / congés / réclamations pour le service « analyse opérationnelle »
/// (pôle pilotage performance).
/// </summary>
internal static class DockerComposePilotageEnrichmentSeed
{
    private const int EnrichmentVersion = 1;
    private const string MarkerAction = "DockerPlanningPilotageEnrichmentV1";

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
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Planning.PilotageEnrichment");

        if (await context.PlanningComments.AnyAsync(c => c.Comment == MarkerAction, ct))
        {
            logger.LogInformation("Planning pilotage enrichment v{Version} déjà appliqué.", EnrichmentVersion);
            return;
        }

        await EnsureRolesAsync(context, ct);
        var sub = await EnsureOrgAndUsersAsync(context, configuration, ct);
        if (sub is null)
        {
            logger.LogWarning("Planning pilotage enrichment ignoré : org / users non créés.");
            return;
        }

        var cellUsers = await context.Users
            .Where(u => u.SubServiceId == sub.Id && u.IsActive)
            .OrderBy(u => u.Id)
            .ToListAsync(ct);

        if (cellUsers.Count == 0)
        {
            logger.LogWarning("Planning pilotage enrichment ignoré : aucun user sur {Sub}.", sub.Name);
            return;
        }

        var manager = cellUsers.FirstOrDefault(u =>
            u.Email.Equals("salim.ouazzani@contactcentre.ma", StringComparison.OrdinalIgnoreCase))
            ?? cellUsers.FirstOrDefault(u =>
                u.Email.Equals("malak.souiri@contactcentre.ma", StringComparison.OrdinalIgnoreCase))
            ?? cellUsers[0];

        await EnsureSaturdayGroupsAsync(context, cellUsers, manager.Id, ct);
        await SeedCongesAsync(context, cellUsers, ct);

        await planningService.SaveShiftTemplateAsync(new SaveShiftConfigDto
        {
            SubServiceId = sub.Id,
            Shifts = BuildShiftConfigItems(cellUsers.Count),
        });

        var weeks = new[] { -2, -1, 0, 1 };
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
            if (offset <= 0)
                await planningService.PublishPlanningAsync(created.Id, manager.Id);
        }

        await SeedReclamationsAsync(context, cellUsers, ct);
        await MarkAppliedAsync(context, sub.Id, manager.Id, ct);

        logger.LogInformation(
            "Planning pilotage enrichment v{Version} : {Users} users, semaines sur « {Sub} ».",
            EnrichmentVersion,
            cellUsers.Count,
            sub.Name);
    }

    private static bool IsEnabled(IConfiguration configuration) =>
        string.Equals(configuration["KYNTUS_PLANNING_DEMO_SEED"], "true", StringComparison.OrdinalIgnoreCase)
        && string.Equals(configuration["KYNTUS_DEMO_ENRICHMENT"] ?? "true", "true", StringComparison.OrdinalIgnoreCase);

    private static async Task EnsureRolesAsync(AppDbContext context, CancellationToken ct)
    {
        var utc = DateTime.UtcNow;
        foreach (var (name, desc) in new (string, string)[]
                 {
                     ("Pilote", "Pilote"),
                     ("Superviseur", "Superviseur de cellule"),
                     ("Référent technique", "Référent technique"),
                     ("Chef de projet", "Chef de projet"),
                 })
        {
            if (await context.Roles.AnyAsync(r => r.Name == name, ct))
                continue;
            context.Roles.Add(new Role { Name = name, Description = desc, IsActive = true, CreatedAt = utc });
        }

        await context.SaveChangesAsync(ct);
    }

    private static async Task<SubService?> EnsureOrgAndUsersAsync(
        AppDbContext context,
        IConfiguration configuration,
        CancellationToken ct)
    {
        var floor = await context.Floors
            .FirstOrDefaultAsync(f => f.PrimePoleId == PilotagePerformanceRoster.PoleId
                || EF.Functions.ILike(f.Name, "%pilotage%performance%"), ct);
        if (floor is null)
        {
            floor = new Floor
            {
                Name = PilotagePerformanceRoster.PoleName,
                FloorNumber = 2,
                Description = "Pôle pilotage performance (Directory)",
                PrimePoleId = PilotagePerformanceRoster.PoleId,
            };
            context.Floors.Add(floor);
            await context.SaveChangesAsync(ct);
        }
        else
        {
            floor.PrimePoleId ??= PilotagePerformanceRoster.PoleId;
            floor.Name = PilotagePerformanceRoster.PoleName;
            await context.SaveChangesAsync(ct);
        }

        var service = await context.Services
            .FirstOrDefaultAsync(s => s.PrimeCelluleId == PilotagePerformanceRoster.CelluleId
                || EF.Functions.ILike(s.Name, "%suivi%KPI%"), ct);
        if (service is null)
        {
            service = new Service
            {
                FloorId = floor.Id,
                Name = PilotagePerformanceRoster.CelluleName,
                Code = "SUIVI-KPI",
                PrimeCelluleId = PilotagePerformanceRoster.CelluleId,
            };
            context.Services.Add(service);
            await context.SaveChangesAsync(ct);
        }
        else
        {
            service.FloorId = floor.Id;
            service.PrimeCelluleId ??= PilotagePerformanceRoster.CelluleId;
            service.Name = PilotagePerformanceRoster.CelluleName;
            await context.SaveChangesAsync(ct);
        }

        var sub = await context.SubServices
            .FirstOrDefaultAsync(s => s.PrimeServiceId == PilotagePerformanceRoster.ServiceId
                || s.Code == PilotagePerformanceRoster.ServiceCode
                || EF.Functions.ILike(s.Name, "%analyse%operationnelle%"), ct);
        if (sub is null)
        {
            sub = new SubService
            {
                ServiceId = service.Id,
                Name = PilotagePerformanceRoster.ServiceName,
                Code = PilotagePerformanceRoster.ServiceCode,
                PrimeServiceId = PilotagePerformanceRoster.ServiceId,
            };
            context.SubServices.Add(sub);
            await context.SaveChangesAsync(ct);
        }
        else
        {
            sub.ServiceId = service.Id;
            sub.PrimeServiceId ??= PilotagePerformanceRoster.ServiceId;
            sub.Name = PilotagePerformanceRoster.ServiceName;
            sub.Code = PilotagePerformanceRoster.ServiceCode;
            await context.SaveChangesAsync(ct);
        }

        var roleByName = await context.Roles.ToDictionaryAsync(r => r.Name, r => r.Id, ct);
        var pwd = BCrypt.Net.BCrypt.HashPassword(
            configuration["DemoSeed:PlanningDemoPassword"] ?? "Azerty@123");
        var hire = DateTime.UtcNow.AddMonths(-8);

        foreach (var emp in PilotagePerformanceRoster.Employees)
        {
            if (!roleByName.TryGetValue(emp.PlanningRole, out var roleId))
                roleId = roleByName["Pilote"];

            var needle = emp.Email.ToLowerInvariant();
            var row = await context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == needle, ct)
                ?? await context.Users.FirstOrDefaultAsync(u => u.Guid == emp.Guid, ct);

            if (row is null)
            {
                context.Users.Add(new User
                {
                    Guid = emp.Guid,
                    FirstName = emp.FirstName,
                    LastName = emp.LastName,
                    Email = emp.Email,
                    RoleId = roleId,
                    SubServiceId = sub.Id,
                    PasswordHash = pwd,
                    HireDate = hire,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                });
            }
            else
            {
                row.Guid = emp.Guid;
                row.FirstName = emp.FirstName;
                row.LastName = emp.LastName;
                row.Email = emp.Email;
                row.RoleId = roleId;
                row.SubServiceId = sub.Id;
                row.IsActive = true;
                if (string.IsNullOrWhiteSpace(row.PasswordHash))
                    row.PasswordHash = pwd;
            }
        }

        await context.SaveChangesAsync(ct);
        return sub;
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

    private static async Task SeedCongesAsync(AppDbContext context, List<User> users, CancellationToken ct)
    {
        var employee = users.FirstOrDefault(u =>
            u.Email.Equals("chaima.benali@contactcentre.ma", StringComparison.OrdinalIgnoreCase))
            ?? users.FirstOrDefault(u =>
                u.Email.Equals("hamid.fellah@contactcentre.ma", StringComparison.OrdinalIgnoreCase))
            ?? users.Last();

        var (_, weekStart) = GetIsoWeek(1);
        var start = weekStart.AddDays(2);
        var end = start.AddDays(1);
        if (await context.Conges.AnyAsync(c => c.UserId == employee.Id && c.StartDate == start, ct))
            return;

        context.Conges.Add(new Conge
        {
            UserId = employee.Id,
            StartDate = start,
            EndDate = end,
            Reason = "Congé — suivi KPI / analyse opérationnelle",
            Status = CongeStatus.Approved,
            AbsenceType = AbsenceType.CongesPayes,
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync(ct);
    }

    private static async Task SeedReclamationsAsync(AppDbContext context, List<User> users, CancellationToken ct)
    {
        var author = users.FirstOrDefault(u =>
            u.Email.Equals("younes.elidrissi@contactcentre.ma", StringComparison.OrdinalIgnoreCase))
            ?? users[0];
        var assignee = users.FirstOrDefault(u =>
            u.Email.Equals("salim.ouazzani@contactcentre.ma", StringComparison.OrdinalIgnoreCase))
            ?? users[0];

        if (await context.Reclamations.AnyAsync(
                r => r.Titre.Contains("pilotage performance"), ct))
            return;

        var now = DateTime.UtcNow;
        context.Reclamations.AddRange(
            new Reclamation
            {
                Titre = "Écart KPI — pôle pilotage performance",
                Description = "Écart constaté sur le tableau de bord KPI de la cellule suivi KPI.",
                Type = ReclamationType.ServiceQualite,
                Status = ReclamationStatus.Soumise,
                Priorite = Priority.Haute,
                AuteurId = author.Guid.ToString(),
                AuteurNom = $"{author.FirstName} {author.LastName}",
                AuteurRole = "Référent technique",
                CreatedAt = now.AddDays(-3),
                UpdatedAt = now.AddDays(-3),
            },
            new Reclamation
            {
                Titre = "Charge planning — service analyse opérationnelle",
                Description = "Demande de rééquilibrage des shifts pour la semaine en cours.",
                Type = ReclamationType.Administrative,
                Status = ReclamationStatus.EnCours,
                Priorite = Priority.Normale,
                AuteurId = author.Guid.ToString(),
                AuteurNom = $"{author.FirstName} {author.LastName}",
                AuteurRole = "Référent technique",
                AssigneeId = assignee.Guid.ToString(),
                AssigneeNom = $"{assignee.FirstName} {assignee.LastName}",
                CreatedAt = now.AddDays(-6),
                UpdatedAt = now.AddDays(-1),
            });
        await context.SaveChangesAsync(ct);
    }

    private static async Task MarkAppliedAsync(AppDbContext context, int subId, int managerId, CancellationToken ct)
    {
        var planning = await context.WeeklyPlannings
            .Where(p => p.SubServiceId == subId)
            .OrderByDescending(p => p.WeekStartDate)
            .FirstOrDefaultAsync(ct);
        if (planning is null)
            return;

        context.PlanningComments.Add(new PlanningComment
        {
            WeeklyPlanningId = planning.Id,
            UserId = managerId,
            CreatedBy = managerId,
            Comment = MarkerAction,
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync(ct);
    }

    private static List<ShiftConfigItemDto> BuildShiftConfigItems(int totalEffectif)
    {
        var perShift = Math.Max(1, totalEffectif / 3);
        return
        [
            new()
            {
                Label = "9h",
                StartTime = "09:00",
                WorkHours = 8,
                BreakDurationMinutes = 60,
                BreakRangeStart = "12:30",
                BreakRangeEnd = "13:30",
                RequiredCount = perShift,
                DisplayOrder = 1,
            },
            new()
            {
                Label = "10h",
                StartTime = "10:00",
                WorkHours = 8,
                BreakDurationMinutes = 60,
                BreakRangeStart = "13:30",
                BreakRangeEnd = "14:30",
                RequiredCount = perShift,
                DisplayOrder = 2,
            },
            new()
            {
                Label = "11h",
                StartTime = "11:00",
                WorkHours = 8,
                BreakDurationMinutes = 60,
                BreakRangeStart = "14:30",
                BreakRangeEnd = "15:30",
                RequiredCount = Math.Max(1, totalEffectif - perShift * 2),
                DisplayOrder = 3,
            },
        ];
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
