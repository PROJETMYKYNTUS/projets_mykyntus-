using DocumentationBackend.Services;
using Kyntus.Messaging.Contracts;
using MassTransit;

namespace DocumentationBackend.Messaging;

public sealed class EmployeDirectorySyncConsumer(DirectoryUserSyncService sync) :
    IConsumer<EmployeCreatedMessage>,
    IConsumer<EmployeUpdatedMessage>
{
    public Task Consume(ConsumeContext<EmployeCreatedMessage> context) =>
        sync.UpsertFromEmployeMessageAsync(context.Message, skipOrgStructureFields: false, context.CancellationToken);

    public Task Consume(ConsumeContext<EmployeUpdatedMessage> context) =>
        sync.UpsertFromEmployeMessageAsync(context.Message, context.Message.SkipOrgStructureFields, context.CancellationToken);
}
