using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Services;

namespace PrimeBackend.Messaging;

/// <summary>Projection org canonique Directory → prime_db (Pôle/Cellule/Service + affectations).</summary>
public sealed class PrimeDirectoryOrgProjectionConsumer(
    PrimeDbContext db,
    PrimeOrgStructureCommandService orgCommands,
    ILogger<PrimeDirectoryOrgProjectionConsumer> logger) :
    IConsumer<DirectoryOrgNodeChangedMessage>,
    IConsumer<DirectoryAssignmentChangedMessage>
{
    public async Task Consume(ConsumeContext<DirectoryOrgNodeChangedMessage> context)
    {
        var msg = context.Message;
        if (msg.IsDeleted) return;

        switch (msg.Level)
        {
            case OrgNodeLevel.Pole:
                await UpsertPoleAsync(msg.NodeId, msg.Name, context.CancellationToken);
                break;
            case OrgNodeLevel.Cellule:
                await UpsertCelluleAsync(msg.NodeId, msg.Name, msg.ParentNodeId, context.CancellationToken);
                break;
            case OrgNodeLevel.Service:
                await UpsertServiceAsync(msg.NodeId, msg.Name, msg.ParentNodeId, context.CancellationToken);
                break;
        }
    }

    public async Task Consume(ConsumeContext<DirectoryAssignmentChangedMessage> context)
    {
        var msg = context.Message;
        if (msg.Removed)
        {
            if (msg.Kind == OrgAssignmentKind.ChefDeProjet)
                await orgCommands.ClearManagerForPoleAsync(msg.NodeId, context.CancellationToken);
            return;
        }

        var userId = msg.EmployeeId.ToString();
        try
        {
            switch (msg.Kind)
            {
                case OrgAssignmentKind.ChefDeProjet:
                    await orgCommands.AssignManagerEtageAsync(userId, msg.NodeId, context.CancellationToken);
                    break;
                case OrgAssignmentKind.Superviseur:
                    await orgCommands.AssignSupervisorServiceAsync(userId, msg.NodeId, context.CancellationToken);
                    break;
                case OrgAssignmentKind.ReferentTechnique:
                    await orgCommands.AssignCoachSousServiceAsync(userId, msg.NodeId, context.CancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Prime Directory assignment {Kind} failed for {UserId}", msg.Kind, userId);
        }
    }

    private async Task UpsertPoleAsync(string id, string name, CancellationToken ct)
    {
        var pole = await db.Poles.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (pole is null)
            db.Poles.Add(new PoleEntity { Id = id.Trim(), Name = name.Trim() });
        else
            pole.Name = name.Trim();
        await db.SaveChangesAsync(ct);
    }

    private async Task UpsertCelluleAsync(string id, string name, string? poleId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(poleId)) return;
        var cell = await db.Cellules.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cell is null)
        {
            db.Cellules.Add(new CelluleEntity
            {
                Id = id.Trim(),
                Name = name.Trim(),
                PoleId = poleId.Trim(),
            });
        }
        else
        {
            cell.Name = name.Trim();
            cell.PoleId = poleId.Trim();
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task UpsertServiceAsync(string id, string name, string? celluleId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(celluleId)) return;
        var svc = await db.Services.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (svc is null)
        {
            db.Services.Add(new ServiceEntity
            {
                Id = id.Trim(),
                Name = name.Trim(),
                CelluleId = celluleId.Trim(),
            });
        }
        else
        {
            svc.Name = name.Trim();
            svc.CelluleId = celluleId.Trim();
        }

        await db.SaveChangesAsync(ct);
    }
}
