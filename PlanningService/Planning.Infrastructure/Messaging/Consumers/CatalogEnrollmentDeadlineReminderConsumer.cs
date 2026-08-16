using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Planning.Infrastructure.Hubs;
using Planning.Infrastructure.Persistence;

namespace Planning.Infrastructure.Messaging.Consumers;

/// <summary>Rappel / escalade échéance catalogue e-learning → cloche Planning.</summary>
public sealed class CatalogEnrollmentDeadlineReminderConsumer(
    AppDbContext db,
    IHubContext<PlanningHub> hubContext,
    ILogger<CatalogEnrollmentDeadlineReminderConsumer> logger) : IConsumer<CatalogEnrollmentDeadlineReminderMessage>
{
    public async Task Consume(ConsumeContext<CatalogEnrollmentDeadlineReminderMessage> context)
    {
        var msg = context.Message;
        if (msg.EmployeeId == Guid.Empty || msg.EnrollmentId == Guid.Empty)
        {
            logger.LogWarning("CatalogEnrollmentDeadlineReminderMessage incomplet.");
            return;
        }

        var title = string.IsNullOrWhiteSpace(msg.CatalogTitle)
            ? "une formation catalogue"
            : $"« {msg.CatalogTitle.Trim()} »";
        var dueLabel = msg.DueAt.ToLocalTime().ToString("dd/MM/yyyy");
        var message = msg.IsEscalation
            ? $"Échéance dépassée pour {title} (prévue le {dueLabel})."
            : $"Rappel : {title} est à terminer avant le {dueLabel}.";
        const string deepLink = "/mes-formations";
        var weekCode = msg.IsEscalation
            ? $"TRAIN-CAT-ESC-{msg.EnrollmentId:N}"
            : $"TRAIN-CAT-DUE-{msg.EnrollmentId:N}";

        await TrainingQuizNotificationHelper.NotifyUserByGuidAsync(
            db,
            hubContext,
            logger,
            msg.EmployeeId,
            weekCode,
            message,
            deepLink,
            $"catalog deadline {msg.EnrollmentId}",
            context.CancellationToken);

        if (msg.IsEscalation && msg.ManagerId is Guid managerId && managerId != Guid.Empty)
        {
            var empName = string.IsNullOrWhiteSpace(msg.EmployeeName) ? "un collaborateur" : msg.EmployeeName.Trim();
            var managerMessage = $"Échéance dépassée : {empName} — {title} (prévue le {dueLabel}).";
            var managerWeek = $"TRAIN-CAT-MGR-{msg.EnrollmentId:N}";
            await TrainingQuizNotificationHelper.NotifyUserByGuidAsync(
                db,
                hubContext,
                logger,
                managerId,
                managerWeek,
                managerMessage,
                deepLink,
                $"catalog deadline manager {msg.EnrollmentId}",
                context.CancellationToken);
        }
    }
}
