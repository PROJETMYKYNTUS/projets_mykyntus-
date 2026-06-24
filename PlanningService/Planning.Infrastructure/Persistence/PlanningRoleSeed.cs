using Microsoft.EntityFrameworkCore;
using Planning.Domain.Entities;

namespace Planning.Infrastructure.Persistence;

internal static class PlanningRoleSeed
{
    internal static async Task EnsureManagerRoleAsync(AppDbContext context, CancellationToken ct = default)
    {
        if (await context.Roles.AnyAsync(r => r.Name == Kyntus.Messaging.Contracts.KyntusRoleNames.Manager, ct))
            return;

        context.Roles.Add(new Role
        {
            Name = Kyntus.Messaging.Contracts.KyntusRoleNames.Manager,
            Description = "Manager département Support",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync(ct);
    }
}
