using Kyntus.Messaging.Contracts;
using MassTransit;

namespace PrimeBackend.Services;

public sealed class OrgStructureEventPublisher(IPublishEndpoint publishEndpoint) : IOrgStructureEventPublisher
{
    public Task PublishNodeCreatedAsync(OrgNodeCreatedMessage message, CancellationToken ct = default) =>
        publishEndpoint.Publish(message, ct);

    public Task PublishNodeRenamedAsync(OrgNodeRenamedMessage message, CancellationToken ct = default) =>
        publishEndpoint.Publish(message, ct);

    public Task PublishAssignmentChangedAsync(OrgAssignmentChangedMessage message, CancellationToken ct = default) =>
        publishEndpoint.Publish(message, ct);
}
