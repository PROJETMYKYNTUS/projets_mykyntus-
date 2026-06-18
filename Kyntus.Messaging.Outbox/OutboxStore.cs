using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Kyntus.Messaging.Outbox;

public sealed class OutboxWriter(DbContext db) : IOutboxWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public Task EnqueueAsync<T>(T message, string? aggregateId = null, string? correlationId = null, CancellationToken ct = default)
        where T : class
    {
        var typeName = typeof(T).FullName ?? typeof(T).Name;
        db.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = typeName,
            PayloadJson = JsonSerializer.Serialize(message, JsonOptions),
            AggregateId = aggregateId,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
        });
        return Task.CompletedTask;
    }
}

public sealed class OutboxReader(DbContext db) : IOutboxReader
{
    public async Task<IReadOnlyList<OutboxMessage>> GetPendingBatchAsync(int batchSize, CancellationToken ct = default) =>
        await db.Set<OutboxMessage>()
            .Where(m => m.ProcessedAt == null && m.RetryCount < 10)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);

    public async Task MarkProcessedAsync(Guid id, CancellationToken ct = default)
    {
        var msg = await db.Set<OutboxMessage>().FirstOrDefaultAsync(m => m.Id == id, ct);
        if (msg is null) return;
        msg.ProcessedAt = DateTime.UtcNow;
        msg.Error = null;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(Guid id, string error, CancellationToken ct = default)
    {
        var msg = await db.Set<OutboxMessage>().FirstOrDefaultAsync(m => m.Id == id, ct);
        if (msg is null) return;
        msg.RetryCount++;
        msg.Error = error.Length > 2000 ? error[..2000] : error;
        await db.SaveChangesAsync(ct);
    }
}
