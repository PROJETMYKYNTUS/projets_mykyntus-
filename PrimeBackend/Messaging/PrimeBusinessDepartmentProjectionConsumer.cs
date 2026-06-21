using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;

namespace PrimeBackend.Messaging;

public sealed class PrimeBusinessDepartmentProjectionConsumer(PrimeDbContext db) :
    IConsumer<DirectoryBusinessDepartmentChangedMessage>
{
    public async Task Consume(ConsumeContext<DirectoryBusinessDepartmentChangedMessage> context)
    {
        var msg = context.Message;
        var id = msg.BusinessDepartmentId.ToString();
        var existing = await db.BusinessDepartments
            .Include(d => d.PoleAssignments)
            .FirstOrDefaultAsync(d => d.Id == id, context.CancellationToken);

        if (msg.IsDeleted)
        {
            if (existing is not null)
            {
                existing.IsActive = false;
                await db.SaveChangesAsync(context.CancellationToken);
            }
            return;
        }

        if (existing is null)
        {
            existing = new BusinessDepartmentEntity { Id = id };
            db.BusinessDepartments.Add(existing);
        }

        existing.Code = msg.Code;
        existing.Name = msg.Name;
        existing.Kind = msg.Kind;
        existing.ManagerEmployeeId = msg.ManagerEmployeeId?.ToString();
        existing.IsActive = true;

        db.BusinessDepartmentPoles.RemoveRange(existing.PoleAssignments);
        existing.PoleAssignments.Clear();
        foreach (var poleId in msg.PoleIds)
        {
            existing.PoleAssignments.Add(new BusinessDepartmentPoleEntity
            {
                Id = Guid.NewGuid(),
                BusinessDepartmentId = id,
                PoleId = poleId,
            });
        }

        await db.SaveChangesAsync(context.CancellationToken);
    }
}
