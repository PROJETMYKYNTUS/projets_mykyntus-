using System.Threading.Channels;
using Planning.Application.DTOs;

namespace Planning.Infrastructure.Services.EmployeeImport;

public sealed record EmployeeImportExecuteWorkItem(
    Guid JobId,
    EmployeeImportExecuteRequest Request,
    string? StartedByEmail,
    string? AuthorizationHeader);

public interface IEmployeeImportExecuteQueue
{
    ValueTask EnqueueAsync(EmployeeImportExecuteWorkItem item, CancellationToken ct = default);
    ValueTask<EmployeeImportExecuteWorkItem> DequeueAsync(CancellationToken ct);
}

public sealed class EmployeeImportExecuteQueue : IEmployeeImportExecuteQueue
{
    private readonly Channel<EmployeeImportExecuteWorkItem> _channel =
        Channel.CreateUnbounded<EmployeeImportExecuteWorkItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    public ValueTask EnqueueAsync(EmployeeImportExecuteWorkItem item, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(item, ct);

    public ValueTask<EmployeeImportExecuteWorkItem> DequeueAsync(CancellationToken ct) =>
        _channel.Reader.ReadAsync(ct);
}
