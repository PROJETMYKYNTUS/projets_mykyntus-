using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Planning.Infrastructure.Hubs;
using Planning.Infrastructure.Persistence;

namespace Planning.Infrastructure.Messaging.Consumers;

/// <summary>Formation catalogue publiée → cloche in-app des concernés.</summary>
public sealed class CatalogFormationAvailableConsumer(
    AppDbContext db,
    IHubContext<PlanningHub> hubContext,
    ILogger<CatalogFormationAvailableConsumer> logger) : IConsumer<CatalogFormationAvailableMessage>
{
    public async Task Consume(ConsumeContext<CatalogFormationAvailableMessage> context)
    {
        var msg = context.Message;
        if (msg.EmployeeId == Guid.Empty || msg.CatalogItemId == Guid.Empty)
        {
            logger.LogWarning("CatalogFormationAvailableMessage incomplet.");
            return;
        }

        var title = string.IsNullOrWhiteSpace(msg.CatalogTitle)
            ? "une formation"
            : $"« {msg.CatalogTitle.Trim()} »";
        var message = $"Nouvelle formation disponible : {title}. Consultez Mes formations.";
        const string deepLink = "/mes-formations";
        var weekCode = $"TRAIN-CAT-AVAIL-{msg.CatalogItemId:N}-{msg.EmployeeId:N}";

        await TrainingQuizNotificationHelper.NotifyUserByGuidAsync(
            db,
            hubContext,
            logger,
            msg.EmployeeId,
            weekCode,
            message,
            deepLink,
            $"catalog available {msg.CatalogItemId}",
            context.CancellationToken);
    }
}
