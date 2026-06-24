using Documentation.Infrastructure.Services;
using Kyntus.Messaging.Contracts;
using MassTransit;

namespace Documentation.Infrastructure.Messaging;

/// <summary>Synchronise directory_users quand Organisation RH change une affectation structurelle.</summary>
public sealed class OrgAssignmentDirectorySyncConsumer(DirectoryUserSyncService sync) :
    IConsumer<OrgAssignmentChangedMessage>
{
    public Task Consume(ConsumeContext<OrgAssignmentChangedMessage> context) =>
        sync.ApplyOrgAssignmentAsync(context.Message, context.CancellationToken);
}
