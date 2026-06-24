using EmployeeDirectory.Infrastructure.Messaging;
using Kyntus.Messaging.Contracts;
using MassTransit;

namespace EmployeeDirectory.Infrastructure.Messaging.Consumers;

public sealed class DirectoryEmployeSyncConsumer(DirectoryEmployeSyncService sync) :
    IConsumer<EmployeCreatedMessage>,
    IConsumer<EmployeUpdatedMessage>
{
    public Task Consume(ConsumeContext<EmployeCreatedMessage> context) =>
        sync.UpsertFromEmployeMessageAsync(
            context.Message.EmployeId,
            context.Message.Prenom,
            context.Message.Nom,
            context.Message.Email,
            context.Message.Role,
            context.Message.PrimeServiceId,
            context.Message.SupervisorId,
            context.CancellationToken);

    public Task Consume(ConsumeContext<EmployeUpdatedMessage> context) =>
        sync.UpsertFromEmployeMessageAsync(
            context.Message.EmployeId,
            context.Message.Prenom,
            context.Message.Nom,
            context.Message.Email,
            context.Message.Role,
            context.Message.PrimeServiceId,
            context.Message.SupervisorId,
            context.CancellationToken);
}

public sealed class DirectoryOrgSyncConsumer(DirectoryOrgSyncService sync) :
    IConsumer<OrgNodeCreatedMessage>,
    IConsumer<OrgNodeRenamedMessage>
{
    public Task Consume(ConsumeContext<OrgNodeCreatedMessage> context) =>
        sync.UpsertNodeCreatedAsync(context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<OrgNodeRenamedMessage> context) =>
        sync.RenameNodeAsync(context.Message, context.CancellationToken);
}

public sealed class DirectoryAssignmentSyncConsumer(DirectoryAssignmentSyncService sync) :
    IConsumer<OrgAssignmentChangedMessage>
{
    public Task Consume(ConsumeContext<OrgAssignmentChangedMessage> context) =>
        sync.ApplyOrgAssignmentAsync(context.Message, context.CancellationToken);
}
