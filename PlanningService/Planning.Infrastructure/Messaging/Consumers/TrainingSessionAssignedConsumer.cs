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
/// Affectation formation continue → notification persistée (+ push SignalR) pour le bénéficiaire.
/// </summary>
public sealed class TrainingSessionAssignedConsumer(
    AppDbContext db,
    IHubContext<PlanningHub> hubContext,
    ILogger<TrainingSessionAssignedConsumer> logger) : IConsumer<TrainingSessionAssignedMessage>
{
    public async Task Consume(ConsumeContext<TrainingSessionAssignedMessage> context)
    {
        var msg = context.Message;
        if (msg.EmployeeId == Guid.Empty || msg.SessionId == Guid.Empty)
        {
            logger.LogWarning("TrainingSessionAssignedMessage incomplet (EmployeeId/SessionId).");
            return;
        }

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Guid == msg.EmployeeId, context.CancellationToken);
        if (user is null)
        {
            logger.LogWarning(
                "Employé {EmployeeId} introuvable pour session formation {SessionId}.",
                msg.EmployeeId,
                msg.SessionId);
            return;
        }

        if (user.AuthUserId is null)
        {
            logger.LogWarning(
                "Employé {EmployeeId} sans AuthUserId — notification formation non créée.",
                msg.EmployeeId);
            return;
        }

        var weekCode = $"FORMATION-{msg.SessionId:N}";
        var startLabel = msg.PlannedStart.ToLocalTime().ToString("g");
        var message = string.IsNullOrWhiteSpace(msg.Title)
            ? $"Vous êtes inscrit à une formation continue (début {startLabel})."
            : $"Vous êtes inscrit à la formation « {msg.Title.Trim()} » (début {startLabel}).";

        var exists = await db.PlanningNotifications.AnyAsync(
            n => n.AuthUserId == user.AuthUserId.Value && n.WeekCode == weekCode && n.UserId == user.Id,
            context.CancellationToken);
        if (!exists)
        {
            db.PlanningNotifications.Add(new PlanningNotification
            {
                UserId = user.Id,
                AuthUserId = user.AuthUserId.Value,
                WeeklyPlanningId = null,
                WeekCode = weekCode,
                SubServiceName = "Formation continue",
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(context.CancellationToken);
        }

        await hubContext.Clients
            .Group($"user_{user.AuthUserId}")
            .SendAsync("PlanningPublished", new
            {
                weekCode,
                subServiceName = "Formation continue",
                message,
                weeklyPlanningId = (int?)null,
            }, context.CancellationToken);

        logger.LogInformation(
            "Notification formation continue créée pour {EmployeeId} (session {SessionId}).",
            msg.EmployeeId,
            msg.SessionId);
    }
}
