using Kyntus.Messaging.Contracts;

namespace Prime.Infrastructure.Services;

public interface IOrgStructureEventPublisher
{
    Task PublishNodeCreatedAsync(OrgNodeCreatedMessage message, CancellationToken ct = default);
    Task PublishNodeRenamedAsync(OrgNodeRenamedMessage message, CancellationToken ct = default);
    Task PublishAssignmentChangedAsync(OrgAssignmentChangedMessage message, CancellationToken ct = default);
}
