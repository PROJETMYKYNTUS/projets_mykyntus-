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
        var msg = context.Message;
        await employeeSync.UpsertAsync(
            EmployeeDirectorySyncService.FromEmployeMessage(new EmployeCreatedMessage
            {
                EmployeId = msg.EmployeId,
                Nom = msg.Nom,
                Prenom = msg.Prenom,
                Email = msg.Email,
                ManagerId = msg.ManagerId,
                ServiceId = msg.ServiceId,
                ServiceNom = msg.ServiceNom,
                DateEmbauche = DateTime.UtcNow,
                EstMineur = false,
                Role = msg.Role,
                SubServiceId = msg.SubServiceId,
                PrimeServiceId = msg.PrimeServiceId,
                SupervisorId = msg.SupervisorId
            }),
            context.CancellationToken);
    }
}
