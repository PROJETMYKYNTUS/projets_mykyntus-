using Kyntus.Messaging.Contracts;
using Kyntus.Messaging.Outbox;

namespace PrimeBackend.Services;

public sealed class OrgStructureEventPublisher(IOutboxWriter outbox) : IOrgStructureEventPublisher
{
    public Task PublishNodeCreatedAsync(OrgNodeCreatedMessage message, CancellationToken ct = default) =>
        outbox.EnqueueAsync(message, message.NodeId, ct: ct);

    public Task PublishNodeRenamedAsync(OrgNodeRenamedMessage message, CancellationToken ct = default) =>
        outbox.EnqueueAsync(message, message.NodeId, ct: ct);

    public Task PublishAssignmentChangedAsync(OrgAssignmentChangedMessage message, CancellationToken ct = default) =>
        outbox.EnqueueAsync(message, message.NodeId, ct: ct);
}
