using Documentation.Infrastructure.Services;
using Kyntus.Messaging.Contracts;
using MassTransit;

namespace Documentation.Infrastructure.Messaging;

public sealed class OrgStructureDirectorySyncConsumer(DirectoryUserSyncService sync) :
    IConsumer<OrgNodeCreatedMessage>,
    IConsumer<OrgNodeRenamedMessage>
{
    public Task Consume(ConsumeContext<OrgNodeCreatedMessage> context) =>
        sync.UpsertOrganisationUnitAsync(context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<OrgNodeRenamedMessage> context)
    {
        var msg = context.Message;
        return sync.UpsertOrganisationUnitAsync(new OrgNodeCreatedMessage
        {
            NodeId = msg.NodeId,
            Name = msg.NewName,
            Level = msg.Level,
            Code = msg.NodeId
        }, context.CancellationToken);
    }
}
