using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Planning.Infrastructure.Persistence;

namespace Planning.Infrastructure.Messaging.Consumers;

/// <summary>
/// Congé refusé ou annulé après validation → retire l'absence Planning liée.
/// </summary>
public sealed class CongeRefuseConsumer(
    AppDbContext db,
    ILogger<CongeRefuseConsumer> logger) : IConsumer<CongeRefuseMessage>
{
    public async Task Consume(ConsumeContext<CongeRefuseMessage> context)
    {
        var msg = context.Message;
        if (msg.DemandeId == Guid.Empty) return;

        var rows = await db.Conges
            .Where(c => c.SourceDemandeId == msg.DemandeId)
            .ToListAsync(context.CancellationToken);

        if (rows.Count == 0) return;

        db.Conges.RemoveRange(rows);
        await db.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation(
            "Absence(s) Planning retirée(s) pour demande {DemandeId} ({Count}) — {Motif}.",
            msg.DemandeId,
            rows.Count,
            msg.Motif);
    }
}
