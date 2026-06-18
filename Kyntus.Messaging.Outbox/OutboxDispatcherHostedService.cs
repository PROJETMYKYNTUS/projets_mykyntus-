using System.Reflection;
using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kyntus.Messaging.Outbox;

public sealed class OutboxDispatcherHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxDispatcherHostedService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Outbox dispatcher batch failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private async Task DispatchBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IOutboxReader>();
        var publish = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var batch = await reader.GetPendingBatchAsync(50, ct);
        foreach (var msg in batch)
        {
            try
            {
                var type = ResolveMessageType(msg.MessageType);
                if (type is null)
                {
                    await reader.MarkFailedAsync(msg.Id, $"Unknown message type: {msg.MessageType}", ct);
                    continue;
                }

                var payload = JsonSerializer.Deserialize(msg.PayloadJson, type, JsonOptions);
                if (payload is null)
                {
                    await reader.MarkFailedAsync(msg.Id, "Empty payload", ct);
                    continue;
                }

                await publish.Publish(payload, ct);
                await reader.MarkProcessedAsync(msg.Id, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Outbox publish failed for {OutboxId}", msg.Id);
                await reader.MarkFailedAsync(msg.Id, ex.Message, ct);
            }
        }
    }

    private static Type? ResolveMessageType(string messageType)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(messageType, throwOnError: false, ignoreCase: false);
            if (t is not null) return t;
            t = asm.GetTypes().FirstOrDefault(x => x.FullName == messageType || x.Name == messageType);
            if (t is not null) return t;
        }
        return Type.GetType(messageType, throwOnError: false);
    }
}

public static class OutboxServiceCollectionExtensions
{
    public static IServiceCollection AddKyntusOutbox<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddScoped<IOutboxWriter, OutboxWriter>(sp =>
            new OutboxWriter(sp.GetRequiredService<TDbContext>()));
        services.AddScoped<IOutboxReader, OutboxReader>(sp =>
            new OutboxReader(sp.GetRequiredService<TDbContext>()));
        services.AddHostedService<OutboxDispatcherHostedService>();
        return services;
    }
}
