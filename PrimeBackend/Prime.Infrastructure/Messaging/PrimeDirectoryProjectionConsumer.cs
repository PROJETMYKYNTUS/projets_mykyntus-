using Kyntus.Messaging.Contracts;
using MassTransit;
using Prime.Infrastructure.Services;

namespace Prime.Infrastructure.Messaging;

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
                SupervisorId: msg.SuperviseurId ?? msg.ParentId ?? Guid.Empty,
                BusinessDepartmentId: msg.BusinessDepartmentId,
                BusinessDepartmentKind: msg.BusinessDepartmentKind,
                ChefDeProjetId: msg.ChefDeProjetId,
                SuperviseurId: msg.SuperviseurId,
                ReferentTechniqueId: msg.ReferentTechniqueId),
            context.CancellationToken);
    }
}
