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
/// Animateur désigné sur une session continue → notification persistée (+ SignalR).
/// </summary>
public sealed class TrainingSessionAnimatorAssignedConsumer(
    AppDbContext db,
    IHubContext<PlanningHub> hubContext,
    ILogger<TrainingSessionAnimatorAssignedConsumer> logger) : IConsumer<TrainingSessionAnimatorAssignedMessage>
{
    public async Task Consume(ConsumeContext<TrainingSessionAnimatorAssignedMessage> context)
    {
        var msg = context.Message;
        if (msg.AnimatorUserId == Guid.Empty || msg.SessionId == Guid.Empty)
        {
            logger.LogWarning("TrainingSessionAnimatorAssignedMessage incomplet.");
            return;
        }

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Guid == msg.AnimatorUserId, context.CancellationToken);
        if (user?.AuthUserId is null)
        {
            logger.LogWarning(
                "Animateur {AnimatorUserId} introuvable ou sans AuthUserId (session {SessionId}).",
                msg.AnimatorUserId,
                msg.SessionId);
            return;
        }

        var weekCode = $"TRAINING-ANIM-{msg.SessionId:N}";
        var startLabel = msg.PlannedStart.ToLocalTime().ToString("g");
        var message = string.IsNullOrWhiteSpace(msg.Title)
            ? $"Vous êtes animateur d'une formation continue (début {startLabel})."
            : $"Vous êtes animateur de la formation « {msg.Title.Trim()} » (début {startLabel}).";
        const string deepLink = "/mes-sessions";
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
            "Notification animateur formation créée pour {AnimatorUserId} (session {SessionId}).",
            msg.AnimatorUserId,
            msg.SessionId);
    }
}
