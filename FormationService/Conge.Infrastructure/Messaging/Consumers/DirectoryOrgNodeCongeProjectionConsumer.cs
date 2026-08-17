using Conge.Domain.Interfaces;
using Conge.Infrastructure.Services;
using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Conge.Infrastructure.Messaging.Consumers;

/// <summary>Miroir org Directory → org_nodes_conge.</summary>
public sealed class DirectoryOrgNodeCongeProjectionConsumer(
    IOrgNodeCongeRepository orgNodes,
    IUnitOfWork unitOfWork,
    DirectoryOrgCatalog catalog,
    ILogger<DirectoryOrgNodeCongeProjectionConsumer> logger) : IConsumer<DirectoryOrgNodeChangedMessage>
{
    public async Task Consume(ConsumeContext<DirectoryOrgNodeChangedMessage> context)
    {
        var msg = context.Message;
        if (string.IsNullOrWhiteSpace(msg.NodeId))
            return;

        if (msg.IsDeleted)
        {
            await orgNodes.MarkDeletedAsync(msg.NodeId, context.CancellationToken);
            await unitOfWork.SaveChangesAsync(context.CancellationToken);
            catalog.InvalidateCache();
            return;
        }

        var level = msg.Level switch
        {
            OrgNodeLevel.Pole => "Pole",
            OrgNodeLevel.Cellule => "Cellule",
            _ => "Service"
        };

        await orgNodes.UpsertAsync(msg.NodeId, msg.Name, level, msg.ParentNodeId, context.CancellationToken);
        await unitOfWork.SaveChangesAsync(context.CancellationToken);
        catalog.InvalidateCache();
        logger.LogInformation("CONGE org node upsert {Id} {Level} {Name}", msg.NodeId, level, msg.Name);
    }
}
