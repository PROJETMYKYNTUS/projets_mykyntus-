using Formation.Infrastructure.Persistence;
using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Formation.Infrastructure.Messaging;

public sealed class FormationEmployeCreatedConsumer(FormationDbContext db) : IConsumer<EmployeCreatedMessage>
{
    public async Task Consume(ConsumeContext<EmployeCreatedMessage> context)
    {
        var msg = context.Message;
        var managerId = msg.SupervisorId != Guid.Empty ? msg.SupervisorId : msg.ManagerId;
        var role = string.IsNullOrWhiteSpace(msg.Role) ? KyntusRoleNames.Employee : msg.Role;
        await FormationEmployeSyncHelper.UpsertCoreAsync(db, msg.EmployeId, msg.Nom, msg.Prenom, msg.Email, role, managerId, skipRoleUpdate: false, context.CancellationToken);
    }
}

public sealed class FormationEmployeUpdatedConsumer(FormationDbContext db) : IConsumer<EmployeUpdatedMessage>
{
    public async Task Consume(ConsumeContext<EmployeUpdatedMessage> context)
    {
        var msg = context.Message;
        var managerId = msg.SupervisorId != Guid.Empty ? msg.SupervisorId : msg.ManagerId;
        var role = string.IsNullOrWhiteSpace(msg.Role) ? KyntusRoleNames.Employee : msg.Role;
        await FormationEmployeSyncHelper.UpsertCoreAsync(db, msg.EmployeId, msg.Nom, msg.Prenom, msg.Email, role, managerId, skipRoleUpdate: msg.SkipOrgStructureFields, context.CancellationToken);
    }
}

public sealed class FormationOrgAssignmentSyncConsumer(
    FormationDbContext db,
    ILogger<FormationOrgAssignmentSyncConsumer> logger) : IConsumer<OrgAssignmentChangedMessage>
{
    public async Task Consume(ConsumeContext<OrgAssignmentChangedMessage> context)
    {
        var msg = context.Message;
        if (msg.Removed || string.IsNullOrWhiteSpace(msg.EmployeeId))
            return;

        if (!Guid.TryParse(msg.EmployeeId.Trim(), out var employeId))
            return;

        var roleName = ResolveRoleName(msg);
        var email = msg.EmployeeEmail?.Trim().ToLowerInvariant();

        var row = await db.EmployeAnnuaires.FirstOrDefaultAsync(
            e => e.EmployeId == employeId || (email != null && e.Email.ToLower() == email),
            context.CancellationToken);

        if (row is null)
        {
            logger.LogWarning("FORMATION OrgAssignment : annuaire absent id={Id}", employeId);
            return;
        }

        row.EmployeId = employeId;
        row.Role = roleName;
        if (!string.IsNullOrWhiteSpace(msg.ParentEmployeeId)
            && Guid.TryParse(msg.ParentEmployeeId.Trim(), out var parentId)
            && parentId != Guid.Empty)
        {
            row.ManagerId = parentId;
        }
        if (email is not null)
            row.Email = email;
        row.DerniereModification = DateTime.UtcNow;

        await db.SaveChangesAsync(context.CancellationToken);
        logger.LogInformation("FORMATION OrgAssignment sync {Email} rôle={Role}", row.Email, roleName);
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

internal static class FormationEmployeSyncHelper
{
    internal static async Task UpsertCoreAsync(
        FormationDbContext db,
        Guid employeId,
        string nom,
        string prenom,
        string emailRaw,
        string role,
        Guid managerId,
        bool skipRoleUpdate,
        CancellationToken ct)
    {
        var email = emailRaw.Trim().ToLowerInvariant();
        var row = await db.EmployeAnnuaires.FirstOrDefaultAsync(
            e => e.EmployeId == employeId || e.Email.ToLower() == email, ct);

        var now = DateTime.UtcNow;
        if (row is null)
        {
            db.EmployeAnnuaires.Add(new Formation.Domain.Entities.EmployeAnnuaire
            {
                Id = Guid.NewGuid(),
                EmployeId = employeId,
                Nom = nom,
                Prenom = prenom,
                Email = email,
                Role = role,
                ManagerId = managerId,
                DerniereModification = now
            });
        }
        else
        {
            row.EmployeId = employeId;
            row.Nom = nom;
            row.Prenom = prenom;
            row.Email = email;
            if (!skipRoleUpdate)
            {
                row.Role = role;
                row.ManagerId = managerId;
            }
            row.DerniereModification = now;
        }

        await db.SaveChangesAsync(ct);
    }
}
