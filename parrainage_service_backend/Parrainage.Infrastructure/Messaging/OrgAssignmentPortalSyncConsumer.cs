using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Parrainage.Infrastructure.Persistence;

namespace Parrainage.Infrastructure.Messaging;

/// <summary>Synchronise le rôle portal_users quand Organisation RH change une affectation structurelle.</summary>
public sealed class OrgAssignmentPortalSyncConsumer(
    ParrainageDbContext db,
    ILogger<OrgAssignmentPortalSyncConsumer> logger) : IConsumer<OrgAssignmentChangedMessage>
{
    public async Task Consume(ConsumeContext<OrgAssignmentChangedMessage> context)
    {
        var msg = context.Message;
        if (msg.Removed || string.IsNullOrWhiteSpace(msg.EmployeeId))
            return;

        var roleName = !string.IsNullOrWhiteSpace(msg.NewRole)
            ? msg.NewRole
            : msg.Kind switch
            {
                OrgAssignmentKind.ChefDeProjet => KyntusRoleNames.ChefDeProjet,
                OrgAssignmentKind.Superviseur => KyntusRoleNames.Superviseur,
                OrgAssignmentKind.ReferentTechnique => KyntusRoleNames.ReferentTechnique,
                OrgAssignmentKind.Pilote => KyntusRoleNames.Pilote,
                _ => KyntusRoleNames.Employee
            };

        var portalRole = KyntusPortalRoleMapping.ToParrainageRole(roleName);
        var employeeId = msg.EmployeeId.Trim();
        var email = msg.EmployeeEmail?.Trim().ToLowerInvariant();

        var row = await db.PortalUsers.FirstOrDefaultAsync(
            u => u.Id == employeeId || (email != null && u.Email.ToLower() == email),
            context.CancellationToken);

        if (row is null)
        {
            logger.LogWarning("PARRAINAGE OrgAssignment : portal_user absent id={Id}", employeeId);
            return;
        }

        row.Id = employeeId;
        row.Role = portalRole;
        if (email is not null)
            row.Email = email;

        await db.SaveChangesAsync(context.CancellationToken);
        logger.LogInformation("PARRAINAGE OrgAssignment sync {Email} rôle={Role}", row.Email, portalRole);
    }
}
