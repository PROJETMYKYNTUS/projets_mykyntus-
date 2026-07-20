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
/// Session continue passée en InProgress → notification destinataire (bénéficiaire ou animateur).
/// </summary>
public sealed class TrainingSessionStartedConsumer(
    AppDbContext db,
    IHubContext<PlanningHub> hubContext,
    ILogger<TrainingSessionStartedConsumer> logger) : IConsumer<TrainingSessionStartedMessage>
{
    public async Task Consume(ConsumeContext<TrainingSessionStartedMessage> context)
    {
        var msg = context.Message;
        if (msg.RecipientUserId == Guid.Empty || msg.SessionId == Guid.Empty)
        {
            logger.LogWarning("TrainingSessionStartedMessage incomplet.");
            return;
        }

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Guid == msg.RecipientUserId, context.CancellationToken);
        if (user?.AuthUserId is null)
        {
            logger.LogWarning(
                "Destinataire {RecipientUserId} introuvable pour démarrage session {SessionId}.",
                msg.RecipientUserId,
                msg.SessionId);
            return;
        }

        var roleKey = string.Equals(msg.RecipientRole, "Animator", StringComparison.OrdinalIgnoreCase)
            ? "ANIM"
            : "BEN";
        var weekCode = $"TRAINING-START-{roleKey}-{msg.SessionId:N}";
        var title = string.IsNullOrWhiteSpace(msg.Title) ? "une formation continue" : $"« {msg.Title.Trim()} »";
        var message = string.Equals(msg.RecipientRole, "Animator", StringComparison.OrdinalIgnoreCase)
            ? $"La formation {title} a démarré — ouvrez l’appel des présences."
            : $"La formation {title} a démarré.";
        var deepLink = string.Equals(msg.RecipientRole, "Animator", StringComparison.OrdinalIgnoreCase)
            ? "/mes-sessions"
            : "/mes-formations";
        const string subServiceName = "Formation continue";

        var notif = await db.PlanningNotifications.FirstOrDefaultAsync(
            n => n.AuthUserId == user.AuthUserId.Value && n.WeekCode == weekCode && n.UserId == user.Id,
            context.CancellationToken);

        if (notif is null)
        {
            notif = new PlanningNotification
            {
                UserId = user.Id,
                AuthUserId = user.AuthUserId.Value,
                WeeklyPlanningId = null,
                WeekCode = weekCode,
                SubServiceName = subServiceName,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
            };
            db.PlanningNotifications.Add(notif);
            await db.SaveChangesAsync(context.CancellationToken);
        }

        await hubContext.Clients
            .Group($"user_{user.AuthUserId}")
            .SendAsync("PlanningPublished", new
            {
                id = notif.Id,
                weekCode,
                subServiceName,
                message,
                weeklyPlanningId = (int?)null,
                deepLink,
                createdAt = notif.CreatedAt,
                isRead = notif.IsRead,
            }, context.CancellationToken);

        logger.LogInformation(
            "Notification démarrage formation pour {RecipientUserId} (session {SessionId}, rôle {Role}).",
            msg.RecipientUserId,
            msg.SessionId,
            msg.RecipientRole);
    }
}
