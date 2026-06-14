using Conge.Domain.Interfaces;
using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Conge.Infrastructure.Messaging.Consumers;

/// <summary>Synchronise le rôle snapshot congés quand Organisation RH change une affectation structurelle.</summary>
public sealed class OrgAssignmentCongeSyncConsumer(
    IEmployeSnapshotRepository employeRepo,
    IUnitOfWork unitOfWork,
    ILogger<OrgAssignmentCongeSyncConsumer> logger) : IConsumer<OrgAssignmentChangedMessage>
{
    public async Task Consume(ConsumeContext<OrgAssignmentChangedMessage> context)
    {
        var msg = context.Message;
        if (msg.Removed || string.IsNullOrWhiteSpace(msg.EmployeeId))
            return;

        if (!Guid.TryParse(msg.EmployeeId.Trim(), out var employeId))
        {
            logger.LogWarning("CONGE OrgAssignment : EmployeeId invalide {Id}", msg.EmployeeId);
            return;
        }

        var roleName = ResolveRoleName(msg);
        var snapshot = await employeRepo.GetByEmployeIdOrEmailAsync(
            employeId,
            msg.EmployeeEmail,
            context.CancellationToken);

        if (snapshot is null)
        {
            logger.LogWarning("CONGE OrgAssignment : snapshot absent id={Id}", employeId);
            return;
        }

        Guid? managerId = null;
        if (!string.IsNullOrWhiteSpace(msg.ParentEmployeeId)
            && Guid.TryParse(msg.ParentEmployeeId.Trim(), out var parentId)
            && parentId != Guid.Empty)
        {
            managerId = parentId;
        }

        snapshot.MettreAJourRole(roleName, managerId);
        employeRepo.Update(snapshot);
        await unitOfWork.SaveChangesAsync(context.CancellationToken);
        logger.LogInformation("CONGE OrgAssignment sync {Email} rôle={Role}", snapshot.Email, roleName);
    }

    private static string ResolveRoleName(OrgAssignmentChangedMessage msg)
    {
        if (!string.IsNullOrWhiteSpace(msg.NewRole))
            return KyntusRoleNames.NormalizePlanningRole(msg.NewRole);

        return msg.Kind switch
        {
            OrgAssignmentKind.ChefDeProjet => KyntusRoleNames.ChefDeProjet,
            OrgAssignmentKind.Superviseur => KyntusRoleNames.Superviseur,
            OrgAssignmentKind.ReferentTechnique => KyntusRoleNames.ReferentTechnique,
            OrgAssignmentKind.Pilote => KyntusRoleNames.Pilote,
            _ => KyntusRoleNames.Employee
        };
    }
}
