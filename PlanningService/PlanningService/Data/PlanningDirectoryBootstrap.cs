using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlanningService.Models;

namespace PlanningService.Data;

/// <summary>
/// Publie les employés Planning existants (seed Docker, imports) vers Directory via RabbitMQ.
/// </summary>
internal static class PlanningDirectoryBootstrap
{
    internal static async Task SyncExistingUsersToDirectoryAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publish = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("PlanningDirectoryBootstrap");

        var users = await db.Users
            .Include(u => u.Role)
            .Include(u => u.SubService)
            .Where(u => u.IsActive)
            .ToListAsync(ct);

        if (users.Count == 0) return;

        var synced = 0;
        foreach (var user in users)
        {
            try
            {
                await PublishEmployeUpdatedAsync(db, publish, user, ct);
                synced++;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Sync Directory ignorée pour {Email}", user.Email);
            }
        }

        log.LogInformation("Planning → Directory bootstrap : {Count}/{Total} employé(s) publié(s).", synced, users.Count);
    }

    private static async Task PublishEmployeUpdatedAsync(
        AppDbContext db,
        IPublishEndpoint publish,
        User user,
        CancellationToken ct)
    {
        string? primeServiceId = null;
        if (user.SubServiceId.HasValue)
        {
            primeServiceId = await db.SubServices.AsNoTracking()
                .Where(ss => ss.Id == user.SubServiceId.Value)
                .Select(ss => ss.PrimeServiceId)
                .FirstOrDefaultAsync(ct);
        }

        var serviceNom = user.SubService?.Name ?? "";
        var serviceId = user.SubServiceId.HasValue
            ? KyntusGuidEncoding.FromIntId(user.SubServiceId.Value)
            : Guid.Empty;

        await publish.Publish(new EmployeUpdatedMessage
        {
            EmployeId = user.Guid,
            Nom = user.LastName,
            Prenom = user.FirstName,
            Email = user.Email,
            ManagerId = Guid.Empty,
            ServiceId = serviceId,
            ServiceNom = serviceNom,
            Role = user.Role?.Name ?? KyntusRoleNames.Employee,
            SubServiceId = user.SubServiceId,
            PrimeServiceId = primeServiceId,
            SupervisorId = Guid.Empty,
            SkipOrgStructureFields = false,
        }, ct);
    }
}
