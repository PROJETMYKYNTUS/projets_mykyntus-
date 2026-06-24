using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure.Persistence;
using Planning.Domain.Entities;
using Planning.Infrastructure.Services;

namespace Planning.Infrastructure.Messaging.Consumers;

public sealed class OrgStructureConsumer(AppDbContext db, ILogger<OrgStructureConsumer> logger) :
    IConsumer<OrgNodeCreatedMessage>,
    IConsumer<OrgNodeRenamedMessage>,
    IConsumer<OrgAssignmentChangedMessage>
{
    public async Task Consume(ConsumeContext<OrgNodeCreatedMessage> context)
    {
        var msg = context.Message;
        switch (msg.Level)
        {
            case OrgNodeLevel.Pole:
                await UpsertFloorAsync(msg.NodeId, msg.Name, msg.Code);
                break;
            case OrgNodeLevel.Cellule:
                await UpsertServiceAsync(msg.NodeId, msg.Name, msg.Code, msg.ParentNodeId);
                break;
            case OrgNodeLevel.Service:
                await UpsertSubServiceAsync(msg.NodeId, msg.Name, msg.Code, msg.ParentNodeId);
                break;
        }
    }

    public async Task Consume(ConsumeContext<OrgNodeRenamedMessage> context)
    {
        var msg = context.Message;
        switch (msg.Level)
        {
            case OrgNodeLevel.Pole:
                var floor = await db.Floors.FirstOrDefaultAsync(f => f.PrimePoleId == msg.NodeId);
                if (floor is not null) floor.Name = msg.NewName;
                break;
            case OrgNodeLevel.Cellule:
                var service = await db.Services.FirstOrDefaultAsync(s => s.PrimeCelluleId == msg.NodeId);
                if (service is not null) service.Name = msg.NewName;
                break;
            case OrgNodeLevel.Service:
                var sub = await db.SubServices.FirstOrDefaultAsync(s => s.PrimeServiceId == msg.NodeId);
                if (sub is not null) sub.Name = msg.NewName;
                break;
        }
        await db.SaveChangesAsync();
    }

    public async Task Consume(ConsumeContext<OrgAssignmentChangedMessage> context)
    {
        var msg = context.Message;
        if (msg.Removed || string.IsNullOrWhiteSpace(msg.EmployeeId))
            return;

        var user = await ResolveUserAsync(msg, context.CancellationToken);
        if (user is null)
        {
            logger.LogWarning(
                "OrgAssignmentChanged : utilisateur introuvable id={EmployeeId} email={Email}",
                msg.EmployeeId,
                msg.EmployeeEmail);
            return;
        }

        var roleName = ResolveRoleName(msg);
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, context.CancellationToken);
        if (role is not null)
            user.RoleId = role.Id;

        await ClearManagedLinksAsync(user.Id, context.CancellationToken);

        if (msg.Kind == OrgAssignmentKind.Superviseur && !string.IsNullOrWhiteSpace(msg.NodeId))
        {
            var planningService = await db.Services
                .FirstOrDefaultAsync(s => s.PrimeCelluleId == msg.NodeId.Trim(), context.CancellationToken);
            if (planningService is not null)
            {
                db.UserManagedServices.Add(new UserManagedService
                {
                    UserId = user.Id,
                    ServiceId = planningService.Id
                });
            }
        }
        else if (msg.Kind == OrgAssignmentKind.ReferentTechnique && !string.IsNullOrWhiteSpace(msg.NodeId))
        {
            var sub = await db.SubServices
                .FirstOrDefaultAsync(s => s.PrimeServiceId == msg.NodeId.Trim(), context.CancellationToken);
            if (sub is not null)
            {
                user.SubServiceId = sub.Id;
                db.UserSubServices.Add(new UserSubService { UserId = user.Id, SubServiceId = sub.Id });
            }
        }
        else if (msg.Kind == OrgAssignmentKind.ChefDeProjet)
        {
            user.SubServiceId = null;
        }

        await db.SaveChangesAsync(context.CancellationToken);
        logger.LogInformation(
            "Planning miroir OrgAssignment {Kind} user={UserId} rôle={Role}",
            msg.Kind,
            user.Id,
            roleName);
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

    private async Task<User?> ResolveUserAsync(OrgAssignmentChangedMessage msg, CancellationToken ct)
    {
        var id = msg.EmployeeId.Trim();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Guid.ToString() == id, ct);
        if (user is not null)
            return user;

        if (!string.IsNullOrWhiteSpace(msg.EmployeeEmail))
        {
            var email = msg.EmployeeEmail.Trim().ToLowerInvariant();
            return await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);
        }

        return null;
    }

    private async Task ClearManagedLinksAsync(int userId, CancellationToken ct)
    {
        var managedSubs = db.UserSubServices.Where(us => us.UserId == userId);
        db.UserSubServices.RemoveRange(managedSubs);
        var managedSvcs = db.UserManagedServices.Where(us => us.UserId == userId);
        db.UserManagedServices.RemoveRange(managedSvcs);
        await db.SaveChangesAsync(ct);
    }

    private async Task UpsertFloorAsync(string primePoleId, string name, string code)
    {
        var floor = await db.Floors.FirstOrDefaultAsync(f => f.PrimePoleId == primePoleId);
        if (floor is null)
        {
            floor = new Floor
            {
                Name = name,
                FloorNumber = await db.Floors.CountAsync() + 1,
                PrimePoleId = primePoleId
            };
            db.Floors.Add(floor);
        }
        else
        {
            floor.Name = name;
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Miroir Floor upsert PrimePoleId={Id}", primePoleId);
    }

    private async Task UpsertServiceAsync(string primeCelluleId, string name, string code, string? parentPoleId)
    {
        if (string.IsNullOrWhiteSpace(parentPoleId)) return;

        var floor = await db.Floors.FirstOrDefaultAsync(f => f.PrimePoleId == parentPoleId);
        if (floor is null)
        {
            floor = new Floor
            {
                Name = $"Pôle {parentPoleId}",
                FloorNumber = await db.Floors.CountAsync() + 1,
                PrimePoleId = parentPoleId
            };
            db.Floors.Add(floor);
            await db.SaveChangesAsync();
        }

        var service = await db.Services.FirstOrDefaultAsync(s => s.PrimeCelluleId == primeCelluleId);
        if (service is null)
        {
            service = new Service
            {
                FloorId = floor.Id,
                Name = name,
                Code = ResolveMirrorCode(code, primeCelluleId, PlanningOrgMirrorCodes.ForCellule),
                PrimeCelluleId = primeCelluleId
            };
            db.Services.Add(service);
        }
        else
        {
            service.Name = name;
            service.FloorId = floor.Id;
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Miroir Service upsert PrimeCelluleId={Id}", primeCelluleId);
    }

    private async Task UpsertSubServiceAsync(string primeServiceId, string name, string code, string? parentCelluleId)
    {
        if (string.IsNullOrWhiteSpace(parentCelluleId)) return;

        var service = await db.Services.FirstOrDefaultAsync(s => s.PrimeCelluleId == parentCelluleId);
        if (service is null)
        {
            logger.LogWarning("SubService miroir : cellule parente {Parent} absente — retry MassTransit", parentCelluleId);
            throw new InvalidOperationException(
                $"Parent cellule {parentCelluleId} not yet mirrored; retry org structure sync.");
        }

        var sub = await db.SubServices.FirstOrDefaultAsync(s => s.PrimeServiceId == primeServiceId);
        if (sub is null)
        {
            sub = new SubService
            {
                ServiceId = service.Id,
                Name = name,
                Code = ResolveMirrorCode(code, primeServiceId, PlanningOrgMirrorCodes.ForLeafService),
                PrimeServiceId = primeServiceId
            };
            db.SubServices.Add(sub);
        }
        else
        {
            sub.Name = name;
            sub.ServiceId = service.Id;
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Miroir SubService upsert PrimeServiceId={Id}", primeServiceId);
    }

    private static string ResolveMirrorCode(string? code, string externalId, Func<string, string> fallback)
    {
        if (!string.IsNullOrWhiteSpace(code) && code.Length <= 20)
            return code;
        return fallback(externalId);
    }
}
