using Kyntus.Messaging.Contracts;

namespace PrimeBackend.Services;

public interface IOrgStructureEventPublisher
{
    Task PublishNodeCreatedAsync(OrgNodeCreatedMessage message, CancellationToken ct = default);
    Task PublishNodeRenamedAsync(OrgNodeRenamedMessage message, CancellationToken ct = default);
    Task PublishAssignmentChangedAsync(OrgAssignmentChangedMessage message, CancellationToken ct = default);
}
