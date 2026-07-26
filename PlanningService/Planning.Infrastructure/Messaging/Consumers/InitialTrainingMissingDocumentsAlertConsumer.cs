using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Planning.Domain.Entities;
using Planning.Infrastructure.Hubs;
using Planning.Infrastructure.Persistence;

namespace Planning.Infrastructure.Messaging.Consumers;

/// <summary>
/// Alerte documents manquants (formation initiale J-7) → notifications RH/Admin.
/// </summary>
public sealed class InitialTrainingMissingDocumentsAlertConsumer(
    AppDbContext db,
    IHubContext<PlanningHub> hubContext,
    ILogger<InitialTrainingMissingDocumentsAlertConsumer> logger)
    : IConsumer<InitialTrainingMissingDocumentsAlertMessage>
{
    private const string SubServiceName = "Formation initiale";

    public async Task Consume(ConsumeContext<InitialTrainingMissingDocumentsAlertMessage> context)
    {
        var msg = context.Message;
        if (msg.TrainingPathId == Guid.Empty || string.IsNullOrWhiteSpace(msg.EmployeeName))
        {
            logger.LogWarning("InitialTrainingMissingDocumentsAlertMessage incomplet.");
            return;
        }

        var missing = msg.MissingDocumentTitles?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList()
                      ?? new List<string>();
        if (missing.Count == 0) return;

        var docsList = string.Join(", ", missing);
        var message =
            $"L’employé {msg.EmployeeName.Trim()} n’a plus qu’une semaine avant la fin de sa période de formation et il lui manque les documents suivants : {docsList}";
        var weekCode = $"INIT-DOCS-{msg.TrainingPathId:N}";
        const string deepLink = "/formations?tab=initial";

        var recipients = await db.Users
            .AsNoTracking()
            .Where(u => u.IsActive
                        && u.AuthUserId != null
                        && u.Role != null
                        && (u.Role.Name.ToLower() == "rh" || u.Role.Name.ToLower() == "admin"))
            .Select(u => new { u.Id, AuthUserId = u.AuthUserId!.Value })
            .ToListAsync(context.CancellationToken);

        foreach (var r in recipients)
        {
            var notif = await db.PlanningNotifications.FirstOrDefaultAsync(
                n => n.AuthUserId == r.AuthUserId && n.WeekCode == weekCode && n.UserId == r.Id,
                context.CancellationToken);

            if (notif is null)
            {
                notif = new PlanningNotification
                {
                    UserId = r.Id,
                    AuthUserId = r.AuthUserId,
                    WeeklyPlanningId = null,
                    WeekCode = weekCode,
                    SubServiceName = SubServiceName,
                    Message = message,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                };
                db.PlanningNotifications.Add(notif);
            }
            else
            {
                notif.Message = message;
                notif.IsRead = false;
                notif.ReadAt = null;
                notif.CreatedAt = DateTime.UtcNow;
            }
        }

        if (recipients.Count > 0)
            await db.SaveChangesAsync(context.CancellationToken);

        foreach (var r in recipients)
        {
            var notif = await db.PlanningNotifications.AsNoTracking()
                .FirstOrDefaultAsync(
                    n => n.AuthUserId == r.AuthUserId && n.WeekCode == weekCode && n.UserId == r.Id,
                    context.CancellationToken);
            if (notif is null) continue;

            await hubContext.Clients
                .Group($"user_{r.AuthUserId}")
                .SendAsync("PlanningPublished", new
                {
                    id = notif.Id,
                    weekCode,
                    subServiceName = SubServiceName,
                    message,
                    weeklyPlanningId = (int?)null,
                    deepLink,
                    createdAt = notif.CreatedAt,
                    isRead = notif.IsRead,
                }, context.CancellationToken);
        }

        logger.LogInformation(
            "Alerte docs formation initiale poussée pour {Employee} ({Count} RH/Admin).",
            msg.EmployeeName,
            recipients.Count);
    }
}
