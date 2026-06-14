using Kyntus.Messaging.Contracts;
using MassTransit;
using PrimeBackend.Services;

namespace PrimeBackend.Messaging;

public sealed class EmployeSyncConsumer(
    IEmployeeDirectorySyncService employeeSync) :
    IConsumer<EmployeCreatedMessage>,
    IConsumer<EmployeUpdatedMessage>
{
    public async Task Consume(ConsumeContext<EmployeCreatedMessage> context)
    {
        await employeeSync.UpsertAsync(
            EmployeeDirectorySyncService.FromEmployeMessage(context.Message),
            context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<EmployeUpdatedMessage> context)
    {
        await employeeSync.UpsertAsync(
            EmployeeDirectorySyncService.FromEmployeMessage(context.Message),
            context.CancellationToken);
    }
}
