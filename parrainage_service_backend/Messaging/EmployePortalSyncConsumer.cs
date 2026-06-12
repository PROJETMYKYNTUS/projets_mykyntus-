using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using ParrainageBackend.Data;
using ParrainageBackend.Models;

namespace ParrainageBackend.Messaging;

public sealed class EmployePortalSyncConsumer(
    ParrainageDbContext db,
    ILogger<EmployePortalSyncConsumer> logger) :
    IConsumer<EmployeCreatedMessage>,
    IConsumer<EmployeUpdatedMessage>
{
    public Task Consume(ConsumeContext<EmployeCreatedMessage> context) =>
        UpsertAsync(context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<EmployeUpdatedMessage> context)
    {
        var msg = context.Message;
        return UpsertAsync(new EmployeCreatedMessage
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

    private async Task UpsertAsync(EmployeCreatedMessage msg, CancellationToken ct)
    {
        var email = msg.Email.Trim().ToLowerInvariant();
        var portalId = msg.EmployeId.ToString();
        var role = KyntusPortalRoleMapping.ToParrainageRole(msg.Role);
        var parentId = msg.SupervisorId != Guid.Empty ? msg.SupervisorId.ToString() : null;
        var displayName = $"{msg.Prenom} {msg.Nom}".Trim();

        var row = await db.PortalUsers.FirstOrDefaultAsync(
            u => u.Email.ToLower() == email || u.Id == portalId, ct);

        if (row is null)
        {
            db.PortalUsers.Add(new ParrainagePortalUserEntity
            {
                Id = portalId,
                Email = email,
                Name = string.IsNullOrWhiteSpace(displayName) ? email : displayName,
                Role = role,
                ParentId = parentId
            });
        }
        else
        {
            row.Id = portalId;
            row.Email = email;
            row.Name = string.IsNullOrWhiteSpace(displayName) ? row.Name : displayName;
            row.Role = role;
            if (parentId is not null)
                row.ParentId = parentId;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("PARRAINAGE portal_users sync {Email} rôle={Role} parent={ParentId}", email, role, parentId);
    }
}
