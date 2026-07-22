using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure.Persistence;
using Planning.Domain.Entities;

namespace Planning.Infrastructure.Messaging.Consumers;

public sealed class DirectoryEmployeeProjectionConsumer(AppDbContext db) :
    IConsumer<DirectoryEmployeeChangedMessage>
{
    public async Task Consume(ConsumeContext<DirectoryEmployeeChangedMessage> context)
    {
        var msg = context.Message;
        if (msg.IsDeleted)
        {
            var del = await db.Users.FirstOrDefaultAsync(u => u.Guid == msg.EmployeeId, context.CancellationToken);
            if (del is not null) del.IsActive = false;
            await db.SaveChangesAsync(context.CancellationToken);
            return;
        }

        var user = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Guid == msg.EmployeeId, context.CancellationToken);

        if (user is null)
        {
            user = await db.Users.FirstOrDefaultAsync(
                u => u.Email.ToLower() == msg.Email.Trim().ToLower(),
                context.CancellationToken);
        }

        var role = await db.Roles.FirstOrDefaultAsync(
            r => r.Name == KyntusRoleNames.NormalizePlanningRole(msg.Role),
            context.CancellationToken);
        if (role is null)
            role = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Employee", context.CancellationToken);

        if (user is null)
        {
            if (role is null) return;
            user = new User
            {
                Guid = msg.EmployeeId,
                Email = msg.Email.Trim(),
                FirstName = msg.FirstName.Trim(),
                LastName = msg.LastName.Trim(),
                RoleId = role.Id,
                IsActive = msg.IsActive,
                HireDate = DateTime.UtcNow,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Azerty@123"),
                IdTechnicien = msg.IdTechnicien,
                HtelCode = msg.HtelCode,
            };
            if (!string.IsNullOrWhiteSpace(msg.ServiceId))
            {
                var sub = await db.SubServices.FirstOrDefaultAsync(
                    s => s.PrimeServiceId == msg.ServiceId,
                    context.CancellationToken);
                if (sub is not null)
                    user.SubServiceId = sub.Id;
            }
            else if (string.Equals(msg.BusinessDepartmentKind, "Support", StringComparison.OrdinalIgnoreCase))
            {
                user.SubServiceId = null;
            }
            db.Users.Add(user);
        }
        else
        {
            user.Guid = msg.EmployeeId;
            user.Email = msg.Email.Trim();
            user.FirstName = msg.FirstName.Trim();
            user.LastName = msg.LastName.Trim();
            user.IsActive = msg.IsActive;
            user.IdTechnicien = msg.IdTechnicien;
            user.HtelCode = msg.HtelCode;
            if (role is not null && !IsStructureRole(user.Role?.Name))
                user.RoleId = role.Id;
            if (!string.IsNullOrWhiteSpace(msg.ServiceId))
            {
                var sub = await db.SubServices.FirstOrDefaultAsync(
                    s => s.PrimeServiceId == msg.ServiceId,
                    context.CancellationToken);
                if (sub is not null) user.SubServiceId = sub.Id;
            }
            else if (string.Equals(msg.BusinessDepartmentKind, "Support", StringComparison.OrdinalIgnoreCase))
            {
                user.SubServiceId = null;
            }
        }

        await db.SaveChangesAsync(context.CancellationToken);

        await SyncManagerIdsAsync(user, msg.ChefDeProjetId, msg.SuperviseurId, msg.ReferentTechniqueId, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }

    private async Task SyncManagerIdsAsync(
        User user,
        Guid? chefDeProjetId,
        Guid? superviseurId,
        Guid? referentTechniqueId,
        CancellationToken ct)
    {
        if (!chefDeProjetId.HasValue && !superviseurId.HasValue && !referentTechniqueId.HasValue)
            return;

        var profile = await db.UserHrProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id, ct);
        if (profile is null)
        {
            profile = new UserHrProfile { UserId = user.Id };
            db.UserHrProfiles.Add(profile);
        }

        if (chefDeProjetId.HasValue) profile.ChefDeProjetId = chefDeProjetId;
        if (superviseurId.HasValue) profile.SuperviseurId = superviseurId;
        if (referentTechniqueId.HasValue) profile.ReferentTechniqueId = referentTechniqueId;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static bool IsStructureRole(string? role) =>
        KyntusRoleNames.IsChefDeProjet(role)
        || KyntusRoleNames.IsSuperviseur(role)
        || KyntusRoleNames.IsReferentTechnique(role);
}
