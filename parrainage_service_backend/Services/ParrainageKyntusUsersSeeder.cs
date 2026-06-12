using Microsoft.EntityFrameworkCore;
using ParrainageBackend.Data;

namespace ParrainageBackend.Services;

internal static class ParrainageKyntusUsersSeeder
{
    internal static async Task SeedPortalUsersAsync(ParrainageDbContext db, ILogger logger, CancellationToken ct)
    {
        if (await db.PortalUsers.AnyAsync(ct))
            return;

        logger.LogInformation(
            "PARRAINAGE : seed portail statique désactivé — les utilisateurs arrivent via EmployePortalSyncConsumer (Planning/RabbitMQ).");
    }
}
