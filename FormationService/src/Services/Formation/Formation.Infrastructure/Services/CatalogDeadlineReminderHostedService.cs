using Formation.Domain.Enums;
using Formation.Infrastructure.Persistence;
using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formation.Infrastructure.Services;

/// <summary>
/// Rappels échéances catalogue (J-3) et escalade manager (échéance dépassée).
/// </summary>
public sealed class CatalogDeadlineReminderHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<CatalogDeadlineReminderHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CatalogDeadlineReminder démarré.");
        await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Erreur job rappels échéances catalogue.");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FormationDbContext>();
        var publish = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var now = DateTime.UtcNow;
        var horizon = now.AddDays(3);
        var reminderCooldown = now.AddDays(-7);

        var dueSoon = await db.TrainingCatalogEnrollments
            .Include(e => e.CatalogItem)
            .Where(e => e.Status != CatalogEnrollmentStatus.Completed
                        && e.DueAt != null
                        && e.DueAt >= now
                        && e.DueAt <= horizon
                        && (e.LastReminderAt == null || e.LastReminderAt < reminderCooldown))
            .ToListAsync(ct);

        foreach (var enrollment in dueSoon)
        {
            var emp = await db.EmployeAnnuaires.AsNoTracking()
                .FirstOrDefaultAsync(e => e.EmployeId == enrollment.EmployeeId, ct);
            var title = enrollment.CatalogItem?.Title ?? "Formation";
            var name = emp is null ? "" : $"{emp.Prenom} {emp.Nom}".Trim();

            await publish.Publish(new CatalogEnrollmentDeadlineReminderMessage
            {
                EnrollmentId = enrollment.Id,
                CatalogItemId = enrollment.CatalogItemId,
                CatalogTitle = title,
                EmployeeId = enrollment.EmployeeId,
                EmployeeName = name,
                DueAt = enrollment.DueAt!.Value,
                IsEscalation = false,
                ManagerId = emp?.ManagerId is Guid mid && mid != Guid.Empty ? mid : null,
                AlertedAt = now,
            }, ct);

            enrollment.LastReminderAt = now;
            enrollment.UpdatedAt = now;
            logger.LogInformation(
                "Rappel échéance catalogue {CatalogId} → employé {EmployeeId} (due {DueAt:u}).",
                enrollment.CatalogItemId, enrollment.EmployeeId, enrollment.DueAt);
        }

        var overdue = await db.TrainingCatalogEnrollments
            .Include(e => e.CatalogItem)
            .Where(e => e.Status != CatalogEnrollmentStatus.Completed
                        && e.DueAt != null
                        && e.DueAt < now
                        && e.EscalatedAt == null)
            .ToListAsync(ct);

        foreach (var enrollment in overdue)
        {
            if (enrollment.Status != CatalogEnrollmentStatus.Overdue)
                enrollment.Status = CatalogEnrollmentStatus.Overdue;

            var emp = await db.EmployeAnnuaires.AsNoTracking()
                .FirstOrDefaultAsync(e => e.EmployeId == enrollment.EmployeeId, ct);
            var title = enrollment.CatalogItem?.Title ?? "Formation";
            var name = emp is null ? "" : $"{emp.Prenom} {emp.Nom}".Trim();

            var managerIds = new HashSet<Guid>();
            // Multi-responsables via annuaire local (pas de dépendance Kyntus.Iam dans Formation.Infrastructure).
            if (!string.IsNullOrWhiteSpace(emp?.CelluleId))
            {
                var celluleId = emp.CelluleId;
                var supervisors = await db.EmployeAnnuaires.AsNoTracking()
                    .Where(e => e.CelluleId == celluleId && e.EmployeId != Guid.Empty)
                    .Select(e => new { e.EmployeId, e.Role })
                    .ToListAsync(ct);
                foreach (var s in supervisors.Where(s => KyntusRoleNames.IsSuperviseur(s.Role)))
                    managerIds.Add(s.EmployeId);
            }

            if (managerIds.Count == 0 && emp?.ManagerId is Guid mid && mid != Guid.Empty)
                managerIds.Add(mid);

            if (managerIds.Count == 0)
            {
                await publish.Publish(new CatalogEnrollmentDeadlineReminderMessage
                {
                    EnrollmentId = enrollment.Id,
                    CatalogItemId = enrollment.CatalogItemId,
                    CatalogTitle = title,
                    EmployeeId = enrollment.EmployeeId,
                    EmployeeName = name,
                    DueAt = enrollment.DueAt!.Value,
                    IsEscalation = true,
                    ManagerId = null,
                    AlertedAt = now,
                }, ct);
            }
            else
            {
                foreach (var managerId in managerIds)
                {
                    await publish.Publish(new CatalogEnrollmentDeadlineReminderMessage
                    {
                        EnrollmentId = enrollment.Id,
                        CatalogItemId = enrollment.CatalogItemId,
                        CatalogTitle = title,
                        EmployeeId = enrollment.EmployeeId,
                        EmployeeName = name,
                        DueAt = enrollment.DueAt!.Value,
                        IsEscalation = true,
                        ManagerId = managerId,
                        AlertedAt = now,
                    }, ct);
                }
            }

            enrollment.EscalatedAt = now;
            enrollment.UpdatedAt = now;
            logger.LogInformation(
                "Escalade échéance catalogue {CatalogId} employé {EmployeeId} → {ManagerCount} responsable(s).",
                enrollment.CatalogItemId, enrollment.EmployeeId, managerIds.Count);
        }

        if (dueSoon.Count > 0 || overdue.Count > 0)
            await db.SaveChangesAsync(ct);
    }
}
