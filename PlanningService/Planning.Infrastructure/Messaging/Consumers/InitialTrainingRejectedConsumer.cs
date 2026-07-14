using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;
using Planning.Application.Abstractions;

namespace Planning.Infrastructure.Messaging.Consumers;

/// <summary>
/// Réagit au rejet d'une formation initiale : sortie complète de l'employé (désactivation + date de sortie).
/// </summary>
public sealed class InitialTrainingRejectedConsumer(
    IUserService userService,
    ILogger<InitialTrainingRejectedConsumer> logger) : IConsumer<InitialTrainingRejectedMessage>
{
    public async Task Consume(ConsumeContext<InitialTrainingRejectedMessage> context)
    {
        var msg = context.Message;
        if (msg.EmployeeId == Guid.Empty)
        {
            logger.LogWarning("InitialTrainingRejectedMessage sans EmployeeId (path {PathId}).", msg.TrainingPathId);
            return;
        }

        var exited = await userService.ExitAfterInitialTrainingRejectionAsync(
            msg.EmployeeId,
            msg.Reason,
            context.CancellationToken);

        if (!exited)
        {
            logger.LogWarning(
                "Employé {EmployeeId} introuvable pour sortie après rejet formation {PathId}.",
                msg.EmployeeId,
                msg.TrainingPathId);
            return;
        }

        logger.LogInformation(
            "Sortie appliquée pour {EmployeeName} ({EmployeeId}) — rejet formation par {RejectedBy}.",
            msg.EmployeeName,
            msg.EmployeeId,
            msg.RejectedBy);
    }
}
