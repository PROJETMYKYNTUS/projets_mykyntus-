using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Parrainage.Infrastructure.Persistence;
using Parrainage.Domain.Entities;

namespace Parrainage.Infrastructure.Messaging;

public sealed class EmployePortalSyncConsumer(
    ParrainageDbContext db,
    ILogger<EmployePortalSyncConsumer> logger) :
    IConsumer<EmployeCreatedMessage>,
    IConsumer<EmployeUpdatedMessage>
{
    public Task Consume(ConsumeContext<EmployeCreatedMessage> context) =>
        UpsertAsync(context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<EmployeUpdatedMessage> context) =>
        UpsertAsync(context.Message, context.CancellationToken);

    private async Task UpsertAsync(EmployeCreatedMessage msg, CancellationToken ct) =>
        await UpsertCoreAsync(msg.EmployeId, msg.Email, msg.Prenom, msg.Nom, msg.Role, msg.SupervisorId, skipRoleUpdate: false, ct);

    private async Task UpsertAsync(EmployeUpdatedMessage msg, CancellationToken ct) =>
        await UpsertCoreAsync(msg.EmployeId, msg.Email, msg.Prenom, msg.Nom, msg.Role, msg.SupervisorId, skipRoleUpdate: msg.SkipOrgStructureFields, ct);

    private async Task UpsertCoreAsync(
        Guid employeId,
        string emailRaw,
        string prenom,
        string nom,
        string planningRole,
        Guid supervisorId,
        bool skipRoleUpdate,
        CancellationToken ct)
    {
        var email = emailRaw.Trim().ToLowerInvariant();
        var portalId = employeId.ToString();
        var role = KyntusPortalRoleMapping.ToParrainageRole(planningRole);
        var parentId = supervisorId != Guid.Empty ? supervisorId.ToString() : null;
        var displayName = $"{prenom} {nom}".Trim();

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
            if (!skipRoleUpdate)
                row.Role = role;
            if (parentId is not null)
                row.ParentId = parentId;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("PARRAINAGE portal_users sync {Email} rôle={Role} parent={ParentId}", email, role, parentId);
    }
}
