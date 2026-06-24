namespace EmployeeDirectory.Application.Contracts;

public interface IDirectoryEventPublisher
{
    Task PublishPendingAsync(CancellationToken ct = default);
}
