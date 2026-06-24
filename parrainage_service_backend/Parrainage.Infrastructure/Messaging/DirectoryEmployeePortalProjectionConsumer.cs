using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Parrainage.Infrastructure.Persistence;
using Parrainage.Domain.Entities;

namespace Parrainage.Infrastructure.Messaging;

/// <summary>Projection canonique Employee Directory (Phase 6).</summary>
public sealed class DirectoryEmployeePortalProjectionConsumer(
    ParrainageDbContext db,
    ILogger<DirectoryEmployeePortalProjectionConsumer> logger) : IConsumer<DirectoryEmployeeChangedMessage>
{
    public async Task Consume(ConsumeContext<DirectoryEmployeeChangedMessage> context)
    {
        var msg = context.Message;
        if (msg.IsDeleted) return;

        var email = msg.Email.Trim().ToLowerInvariant();
        var portalId = msg.EmployeeId.ToString();
        var role = KyntusPortalRoleMapping.ToParrainageRole(msg.Role);
        var parentId = msg.ParentId?.ToString();
        var displayName = $"{msg.FirstName} {msg.LastName}".Trim();

        var row = await db.PortalUsers.FirstOrDefaultAsync(
            u => u.Email.ToLower() == email || u.Id == portalId, context.CancellationToken);

        if (row is null)
        {
            db.PortalUsers.Add(new ParrainagePortalUserEntity
            {
                Id = portalId,
                Email = email,
                Name = string.IsNullOrWhiteSpace(displayName) ? email : displayName,
                Role = role,
                ParentId = parentId,
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

        await db.SaveChangesAsync(context.CancellationToken);
        logger.LogInformation("PARRAINAGE Directory projection {Email} rôle={Role}", email, role);
    }
}
