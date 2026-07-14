using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;
using Planning.Application.Abstractions;

namespace Planning.Infrastructure.Messaging.Consumers;

/// <summary>
/// Réagit à la validation RH du passage en production : clear EnFormation, expertise Débutant.
/// </summary>
public sealed class InitialTrainingCompletedConsumer(
    IUserService userService,
    ILogger<InitialTrainingCompletedConsumer> logger) : IConsumer<InitialTrainingCompletedMessage>
{
    public async Task Consume(ConsumeContext<InitialTrainingCompletedMessage> context)
    {
        var msg = context.Message;
        if (msg.EmployeeId == Guid.Empty)
        {
            logger.LogWarning("InitialTrainingCompletedMessage sans EmployeeId (path {PathId}).", msg.TrainingPathId);
            return;
        }

        var ok = await userService.CompleteInitialTrainingAsync(
            msg.EmployeeId,
            msg.NiveauExpertiseMetier,
            msg.ProductionStartDate,
            context.CancellationToken);

        if (!ok)
        {
            logger.LogWarning(
                "Employé {EmployeeId} introuvable pour passage production formation {PathId}.",
                msg.EmployeeId,
                msg.TrainingPathId);
            return;
        }

        logger.LogInformation(
            "Passage production appliqué pour {EmployeeName} ({EmployeeId}) — formation {PathId}.",
            msg.EmployeeName,
            msg.EmployeeId,
            msg.TrainingPathId);
    }
}
