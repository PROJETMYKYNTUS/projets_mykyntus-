using Formation.Infrastructure.Persistence;
using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Formation.Infrastructure.Messaging;

public sealed class FormationDirectoryEmployeeProjectionConsumer(FormationDbContext db) :
    IConsumer<DirectoryEmployeeChangedMessage>
{
    public async Task Consume(ConsumeContext<DirectoryEmployeeChangedMessage> context)
    {
        var msg = context.Message;
        if (msg.IsDeleted || !msg.IsActive)
        {
            var row = await db.EmployeAnnuaires.FirstOrDefaultAsync(
                e => e.EmployeId == msg.EmployeeId, context.CancellationToken);
            if (row is not null)
            {
                db.EmployeAnnuaires.Remove(row);
                await db.SaveChangesAsync(context.CancellationToken);
            }
            return;
        }

        await FormationEmployeSyncHelper.UpsertCoreAsync(
            db,
            msg.EmployeeId,
            msg.LastName,
            msg.FirstName,
            msg.Email,
            msg.Role,
            msg.ParentId ?? Guid.Empty,
            skipRoleUpdate: false,
            orgPath: new OrgPath(
                msg.BusinessDepartmentId?.ToString(),
                msg.PoleId,
                msg.CelluleId,
                msg.ServiceId),
            ct: context.CancellationToken);
    }
}

public sealed class FormationEmployeCreatedConsumer(FormationDbContext db) : IConsumer<EmployeCreatedMessage>
{
    public async Task Consume(ConsumeContext<EmployeCreatedMessage> context)
    {
        var msg = context.Message;
        var managerId = msg.SupervisorId != Guid.Empty ? msg.SupervisorId : msg.ManagerId;
        var role = string.IsNullOrWhiteSpace(msg.Role) ? KyntusRoleNames.Employee : msg.Role;
        await FormationEmployeSyncHelper.UpsertCoreAsync(
            db, msg.EmployeId, msg.Nom, msg.Prenom, msg.Email, role, managerId,
            skipRoleUpdate: false,
            orgPath: new OrgPath(null, null, null, msg.PrimeServiceId),
            ct: context.CancellationToken);
    }
}

public sealed class FormationEmployeUpdatedConsumer(FormationDbContext db) : IConsumer<EmployeUpdatedMessage>
{
    public async Task Consume(ConsumeContext<EmployeUpdatedMessage> context)
    {
        var msg = context.Message;
        var managerId = msg.SupervisorId != Guid.Empty ? msg.SupervisorId : msg.ManagerId;
        var role = string.IsNullOrWhiteSpace(msg.Role) ? KyntusRoleNames.Employee : msg.Role;
        await FormationEmployeSyncHelper.UpsertCoreAsync(
            db, msg.EmployeId, msg.Nom, msg.Prenom, msg.Email, role, managerId,
            skipRoleUpdate: msg.SkipOrgStructureFields,
            orgPath: msg.SkipOrgStructureFields ? null : new OrgPath(null, null, null, msg.PrimeServiceId),
            skipStructureUpdate: msg.SkipOrgStructureFields,
            ct: context.CancellationToken);
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
        // NodeId = pôle / cellule / service selon NodeLevel — chemin org + clé d'audience catalogue.
        if (!string.IsNullOrWhiteSpace(msg.NodeId))
        {
            var nodeId = msg.NodeId.Trim();
            switch (msg.NodeLevel)
            {
                case OrgNodeLevel.Pole:
                    row.PoleId = nodeId;
                    break;
                case OrgNodeLevel.Cellule:
                    row.CelluleId = nodeId;
                    break;
                case OrgNodeLevel.Service:
                    row.ServiceId = nodeId;
                    break;
            }
            row.StructureKey = nodeId;
        }
        row.DerniereModification = DateTime.UtcNow;

        await db.SaveChangesAsync(context.CancellationToken);
        logger.LogInformation(
            "FORMATION OrgAssignment sync {Email} rôle={Role} structure={Structure}",
            row.Email, roleName, row.StructureKey);
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

internal sealed record OrgPath(
    string? DepartmentId,
    string? PoleId,
    string? CelluleId,
    string? ServiceId);

internal static class FormationEmployeSyncHelper
{
    internal static string? ResolveStructureKey(OrgPath? path)
    {
        if (path is null) return null;
        if (!string.IsNullOrWhiteSpace(path.ServiceId)) return path.ServiceId.Trim();
        if (!string.IsNullOrWhiteSpace(path.CelluleId)) return path.CelluleId.Trim();
        if (!string.IsNullOrWhiteSpace(path.PoleId)) return path.PoleId.Trim();
        if (!string.IsNullOrWhiteSpace(path.DepartmentId)) return path.DepartmentId.Trim();
        return null;
    }

    internal static async Task UpsertCoreAsync(
        FormationDbContext db,
        Guid employeId,
        string nom,
        string prenom,
        string emailRaw,
        string role,
        Guid managerId,
        bool skipRoleUpdate,
        CancellationToken ct,
        OrgPath? orgPath = null,
        bool skipStructureUpdate = false)
    {
        var email = emailRaw.Trim().ToLowerInvariant();
        var row = await db.EmployeAnnuaires.FirstOrDefaultAsync(
            e => e.EmployeId == employeId || e.Email.ToLower() == email, ct);

        var now = DateTime.UtcNow;
        if (row is null)
        {
            var entity = new Formation.Domain.Entities.EmployeAnnuaire
            {
                Id = Guid.NewGuid(),
                EmployeId = employeId,
                Nom = nom,
                Prenom = prenom,
                Email = email,
                Role = role,
                ManagerId = managerId,
                DerniereModification = now
            };
            if (!skipStructureUpdate && orgPath is not null)
                ApplyOrgPath(entity, orgPath);
            db.EmployeAnnuaires.Add(entity);
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
            if (!skipStructureUpdate && orgPath is not null)
                ApplyOrgPath(row, orgPath);
            row.DerniereModification = now;
        }

        await db.SaveChangesAsync(ct);
    }

    private static void ApplyOrgPath(Formation.Domain.Entities.EmployeAnnuaire row, OrgPath orgPath)
    {
        row.DepartmentId = TrimOrNull(orgPath.DepartmentId);
        row.PoleId = TrimOrNull(orgPath.PoleId);
        row.CelluleId = TrimOrNull(orgPath.CelluleId);
        row.ServiceId = TrimOrNull(orgPath.ServiceId);
        row.StructureKey = ResolveStructureKey(orgPath);
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
