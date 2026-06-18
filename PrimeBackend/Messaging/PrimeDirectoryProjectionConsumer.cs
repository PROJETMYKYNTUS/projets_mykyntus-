using Kyntus.Messaging.Contracts;
using MassTransit;
using PrimeBackend.Services;

namespace PrimeBackend.Messaging;

public sealed class PrimeDirectoryProjectionConsumer(IEmployeeDirectorySyncService employeeSync) :
    IConsumer<DirectoryEmployeeChangedMessage>
{
    public async Task Consume(ConsumeContext<DirectoryEmployeeChangedMessage> context)
    {
        var msg = context.Message;
        if (msg.IsDeleted) return;

        await employeeSync.EnsureFromPlanningAsync(
            new EmployeeDirectoryUpsertRequest(
                EmployeeId: msg.EmployeeId,
                FirstName: msg.FirstName,
                LastName: msg.LastName,
                Email: msg.Email,
                Role: msg.Role,
                PrimeServiceId: msg.ServiceId,
                SupervisorId: msg.ParentId ?? Guid.Empty),
            context.CancellationToken);
    }
}
