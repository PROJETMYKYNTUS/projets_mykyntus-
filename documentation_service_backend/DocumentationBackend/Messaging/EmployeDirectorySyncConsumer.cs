using DocumentationBackend.Services;
using Kyntus.Messaging.Contracts;
using MassTransit;

namespace DocumentationBackend.Messaging;

public sealed class EmployeDirectorySyncConsumer(DirectoryUserSyncService sync) :
    IConsumer<EmployeCreatedMessage>,
    IConsumer<EmployeUpdatedMessage>
{
    public Task Consume(ConsumeContext<EmployeCreatedMessage> context) =>
        sync.UpsertFromEmployeMessageAsync(context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<EmployeUpdatedMessage> context)
    {
        var msg = context.Message;
        return sync.UpsertFromEmployeMessageAsync(new EmployeCreatedMessage
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
        }, context.CancellationToken);
    }
}
