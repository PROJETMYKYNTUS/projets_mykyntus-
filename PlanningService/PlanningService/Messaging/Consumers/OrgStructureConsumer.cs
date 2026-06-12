using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using PlanningService.Data;
using PlanningService.Models;

namespace PlanningService.Messaging.Consumers;

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

    public Task Consume(ConsumeContext<OrgAssignmentChangedMessage> context) =>
        Task.CompletedTask;

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
                Code = string.IsNullOrWhiteSpace(code) ? $"CELL-{primeCelluleId}" : code,
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
            logger.LogWarning("SubService miroir ignoré : cellule parente {Parent} absente", parentCelluleId);
            return;
        }

        var sub = await db.SubServices.FirstOrDefaultAsync(s => s.PrimeServiceId == primeServiceId);
        if (sub is null)
        {
            sub = new SubService
            {
                ServiceId = service.Id,
                Name = name,
                Code = string.IsNullOrWhiteSpace(code) ? $"SVC-{primeServiceId}" : code,
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
}
