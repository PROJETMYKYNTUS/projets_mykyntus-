using Documentation.Infrastructure.Services;
using Kyntus.Messaging.Contracts;
using MassTransit;

namespace Documentation.Infrastructure.Messaging;

/// <summary>Projection canonique Employee Directory (Phase 6 — remplace EmployeCreated/Updated).</summary>
public sealed class DirectoryEmployeeDocumentationProjectionConsumer(DirectoryUserSyncService sync) :
    IConsumer<DirectoryEmployeeChangedMessage>
{
    public Task Consume(ConsumeContext<DirectoryEmployeeChangedMessage> context)
    {
        var msg = context.Message;
        if (msg.IsDeleted)
            return Task.CompletedTask;

        return sync.UpsertFromEmployeMessageAsync(new EmployeCreatedMessage
        {
            EmployeId = msg.EmployeeId,
            Nom = msg.LastName,
            Prenom = msg.FirstName,
            Email = msg.Email,
            ManagerId = msg.ParentId ?? Guid.Empty,
            SupervisorId = msg.ParentId ?? Guid.Empty,
            ServiceId = Guid.TryParse(msg.ServiceId, out var svc) ? svc : Guid.Empty,
            ServiceNom = msg.ServiceId ?? string.Empty,
            DateEmbauche = DateTime.UtcNow,
            Role = msg.Role,
            PrimeServiceId = msg.ServiceId,
        }, skipOrgStructureFields: false, context.CancellationToken);
    }
}
