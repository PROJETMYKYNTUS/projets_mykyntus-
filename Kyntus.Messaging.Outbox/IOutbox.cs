namespace Kyntus.Messaging.Outbox;

public interface IOutboxDbContext
{
    void AddOutboxMessage(OutboxMessage message);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IOutboxWriter
{
    Task EnqueueAsync<T>(T message, string? aggregateId = null, string? correlationId = null, CancellationToken ct = default)
        where T : class;
}

public interface IOutboxReader
{
    Task<IReadOnlyList<OutboxMessage>> GetPendingBatchAsync(int batchSize, CancellationToken ct = default);
    Task MarkProcessedAsync(Guid id, CancellationToken ct = default);
    Task MarkFailedAsync(Guid id, string error, CancellationToken ct = default);
}
