using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Planning.Domain.Entities;
using Planning.Infrastructure.Hubs;
using Planning.Infrastructure.Persistence;

namespace Planning.Infrastructure.Messaging.Consumers;

internal static class TrainingQuizNotificationHelper
{
    public static async Task NotifyUserByGuidAsync(
        AppDbContext db,
        IHubContext<PlanningHub> hubContext,
        ILogger logger,
        Guid userGuid,
        string weekCode,
        string message,
        string deepLink,
        string logContext,
        CancellationToken ct)
    {
        if (userGuid == Guid.Empty)
            return;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Guid == userGuid, ct);
        if (user?.AuthUserId is null)
        {
            logger.LogWarning("Utilisateur {UserGuid} introuvable ou sans AuthUserId ({Context}).", userGuid, logContext);
            return;
        }

        const string subServiceName = "Formation continue";
        var notif = await db.PlanningNotifications.FirstOrDefaultAsync(
            n => n.AuthUserId == user.AuthUserId.Value && n.WeekCode == weekCode && n.UserId == user.Id,
            ct);

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
            await db.SaveChangesAsync(ct);
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
            }, ct);
    }
}

/// <summary>Quiz publié → notification des bénéficiaires affectés.</summary>
public sealed class TrainingQuizPublishedConsumer(
    AppDbContext db,
    IHubContext<PlanningHub> hubContext,
    ILogger<TrainingQuizPublishedConsumer> logger) : IConsumer<TrainingQuizPublishedMessage>
{
    public async Task Consume(ConsumeContext<TrainingQuizPublishedMessage> context)
    {
        var msg = context.Message;
        if (msg.SessionId == Guid.Empty || msg.EmployeeIds.Count == 0)
        {
            logger.LogWarning("TrainingQuizPublishedMessage incomplet.");
            return;
        }

        var title = string.IsNullOrWhiteSpace(msg.Title) ? "un quiz" : $"« {msg.Title.Trim()} »";
        var message = $"Un quiz est disponible : {title}.";
        var deepLink = "/mes-formations";

        foreach (var employeeId in msg.EmployeeIds.Distinct())
        {
            var weekCode = $"TRAIN-QUIZ-PUB-{msg.QuizId:N}-{employeeId:N}";
            await TrainingQuizNotificationHelper.NotifyUserByGuidAsync(
                db, hubContext, logger, employeeId, weekCode, message, deepLink,
                $"quiz published {msg.QuizId}", context.CancellationToken);
        }
    }
}

/// <summary>Tentative soumise → info animateur (via NeedsGrading si free-text ; sinon log + notif légère).</summary>
public sealed class TrainingQuizAttemptSubmittedConsumer(
    AppDbContext db,
    IHubContext<PlanningHub> hubContext,
    ILogger<TrainingQuizAttemptSubmittedConsumer> logger) : IConsumer<TrainingQuizAttemptSubmittedMessage>
{
    public async Task Consume(ConsumeContext<TrainingQuizAttemptSubmittedMessage> context)
    {
        var msg = context.Message;
        logger.LogInformation(
            "Quiz attempt submitted: Session={SessionId} Attempt={AttemptId} Employee={EmployeeId} Graded={IsGraded}",
            msg.SessionId, msg.AttemptId, msg.EmployeeId, msg.IsGraded);

        // Notification employé uniquement si déjà noté (QCM auto) — ResultReady gère le cas principal.
        if (msg.SessionId == Guid.Empty || msg.EmployeeId == Guid.Empty)
            return;

        await Task.CompletedTask;
        _ = db;
        _ = hubContext;
    }
}

/// <summary>Réponses libres à noter → notification animateur.</summary>
public sealed class TrainingQuizNeedsGradingConsumer(
    AppDbContext db,
    IHubContext<PlanningHub> hubContext,
    ILogger<TrainingQuizNeedsGradingConsumer> logger) : IConsumer<TrainingQuizNeedsGradingMessage>
{
    public async Task Consume(ConsumeContext<TrainingQuizNeedsGradingMessage> context)
    {
        var msg = context.Message;
        if (msg.AnimatorUserId == Guid.Empty || msg.SessionId == Guid.Empty)
        {
            logger.LogWarning("TrainingQuizNeedsGradingMessage incomplet.");
            return;
        }

        var who = string.IsNullOrWhiteSpace(msg.EmployeeName) ? "un collaborateur" : msg.EmployeeName.Trim();
        var title = string.IsNullOrWhiteSpace(msg.Title) ? "quiz" : msg.Title.Trim();
        var message = $"Réponses libres à noter ({who}) — {title}.";
        var deepLink = $"/mes-sessions/{msg.SessionId}/quiz";
        var weekCode = $"TRAIN-QUIZ-GRADE-{msg.AttemptId:N}";

        await TrainingQuizNotificationHelper.NotifyUserByGuidAsync(
            db, hubContext, logger, msg.AnimatorUserId, weekCode, message, deepLink,
            $"needs grading {msg.AttemptId}", context.CancellationToken);
    }
}

/// <summary>Quiz validé → bénéficiaires.</summary>
public sealed class TrainingQuizValidatedConsumer(
    AppDbContext db,
    IHubContext<PlanningHub> hubContext,
    ILogger<TrainingQuizValidatedConsumer> logger) : IConsumer<TrainingQuizValidatedMessage>
{
    public async Task Consume(ConsumeContext<TrainingQuizValidatedMessage> context)
    {
        var msg = context.Message;
        if (msg.SessionId == Guid.Empty || msg.EmployeeIds.Count == 0)
        {
            logger.LogWarning("TrainingQuizValidatedMessage incomplet.");
            return;
        }

        var title = string.IsNullOrWhiteSpace(msg.Title) ? "quiz" : msg.Title.Trim();
        var message = $"Le quiz « {title} » a été validé.";
        const string deepLink = "/mes-formations";

        foreach (var employeeId in msg.EmployeeIds.Distinct())
        {
            var weekCode = $"TRAIN-QUIZ-OK-{msg.QuizId:N}-{employeeId:N}";
            await TrainingQuizNotificationHelper.NotifyUserByGuidAsync(
                db, hubContext, logger, employeeId, weekCode, message, deepLink,
                $"quiz validated {msg.QuizId}", context.CancellationToken);
        }
    }
}

/// <summary>Quiz rejeté → bénéficiaires (+ motif).</summary>
public sealed class TrainingQuizRejectedConsumer(
    AppDbContext db,
    IHubContext<PlanningHub> hubContext,
    ILogger<TrainingQuizRejectedConsumer> logger) : IConsumer<TrainingQuizRejectedMessage>
{
    public async Task Consume(ConsumeContext<TrainingQuizRejectedMessage> context)
    {
        var msg = context.Message;
        if (msg.SessionId == Guid.Empty)
        {
            logger.LogWarning("TrainingQuizRejectedMessage incomplet.");
            return;
        }

        var title = string.IsNullOrWhiteSpace(msg.Title) ? "quiz" : msg.Title.Trim();
        var reason = string.IsNullOrWhiteSpace(msg.Reason) ? "" : $" Motif : {msg.Reason.Trim()}";
        var message = $"Le quiz « {title} » a été rejeté.{reason}";
        const string deepLink = "/mes-formations";

        foreach (var employeeId in msg.EmployeeIds.Distinct())
        {
            var weekCode = $"TRAIN-QUIZ-KO-{msg.QuizId:N}-{employeeId:N}-{msg.RejectedAt:yyyyMMddHHmmss}";
            await TrainingQuizNotificationHelper.NotifyUserByGuidAsync(
                db, hubContext, logger, employeeId, weekCode, message, deepLink,
                $"quiz rejected {msg.QuizId}", context.CancellationToken);
        }
    }
}

/// <summary>Résultat noté prêt → employé.</summary>
public sealed class TrainingQuizResultReadyConsumer(
    AppDbContext db,
    IHubContext<PlanningHub> hubContext,
    ILogger<TrainingQuizResultReadyConsumer> logger) : IConsumer<TrainingQuizResultReadyMessage>
{
    public async Task Consume(ConsumeContext<TrainingQuizResultReadyMessage> context)
    {
        var msg = context.Message;
        if (msg.EmployeeId == Guid.Empty || msg.AttemptId == Guid.Empty)
        {
            logger.LogWarning("TrainingQuizResultReadyMessage incomplet.");
            return;
        }

        var title = string.IsNullOrWhiteSpace(msg.Title) ? "quiz" : msg.Title.Trim();
        var scorePart = msg.FinalScore is decimal s ? $" Score : {s:0.#} %." : "";
        var passPart = msg.Passed is bool p ? (p ? " Réussi." : " Non réussi.") : "";
        var message = $"Votre résultat au quiz « {title} » est disponible.{scorePart}{passPart}";
        const string deepLink = "/mes-formations";
        var weekCode = $"TRAIN-QUIZ-RES-{msg.AttemptId:N}";

        await TrainingQuizNotificationHelper.NotifyUserByGuidAsync(
            db, hubContext, logger, msg.EmployeeId, weekCode, message, deepLink,
            $"result ready {msg.AttemptId}", context.CancellationToken);
    }
}
