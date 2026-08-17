using Conge.Domain.Interfaces;
using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Conge.Infrastructure.Messaging.Consumers;

/// <summary>Affectations structurelles Directory → snapshot Congés (rôle + nœud org).</summary>
public sealed class DirectoryAssignmentCongeSyncConsumer(
    IEmployeSnapshotRepository employeRepo,
    IUnitOfWork unitOfWork,
    ILogger<DirectoryAssignmentCongeSyncConsumer> logger) : IConsumer<DirectoryAssignmentChangedMessage>
{
    public async Task Consume(ConsumeContext<DirectoryAssignmentChangedMessage> context)
    {
        var msg = context.Message;
        var snapshot = await employeRepo.GetByEmployeIdOrEmailAsync(
            msg.EmployeeId,
            msg.EmployeeEmail,
            context.CancellationToken);

        if (snapshot is null)
        {
            logger.LogWarning("CONGE DirectoryAssignment : snapshot absent id={Id}", msg.EmployeeId);
            return;
        }

        if (msg.Removed)
        {
            snapshot.MettreAJourRole(KyntusRoleNames.Employee);
            employeRepo.Update(snapshot);
            await unitOfWork.SaveChangesAsync(context.CancellationToken);
            logger.LogInformation("CONGE DirectoryAssignment removed → Employee pour {Email}", snapshot.Email);
            return;
        }

        var roleName = ResolveRoleName(msg);
        snapshot.MettreAJourRole(roleName);
        ApplyNode(snapshot, msg.NodeLevel, msg.NodeId);
        employeRepo.Update(snapshot);
        await unitOfWork.SaveChangesAsync(context.CancellationToken);
        logger.LogInformation(
            "CONGE DirectoryAssignment sync {Email} rôle={Role} node={Node}",
            snapshot.Email, roleName, msg.NodeId);
    }

    private static void ApplyNode(Domain.Entities.EmployeSnapshot snapshot, OrgNodeLevel level, string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return;
        var id = nodeId.Trim();
        switch (level)
        {
            case OrgNodeLevel.Pole:
                snapshot.AppliquerNoeudOrg(id, null, null);
                break;
            case OrgNodeLevel.Cellule:
                snapshot.AppliquerNoeudOrg(null, id, null);
                break;
            case OrgNodeLevel.Service:
                snapshot.AppliquerNoeudOrg(null, null, id);
                break;
        }
    }

    private static string ResolveRoleName(DirectoryAssignmentChangedMessage msg)
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
