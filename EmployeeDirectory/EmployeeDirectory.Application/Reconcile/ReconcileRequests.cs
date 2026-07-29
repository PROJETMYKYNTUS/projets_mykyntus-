using EmployeeDirectory.Application.Abstractions;
using EmployeeDirectory.Application.Dtos;
using MediatR;

namespace EmployeeDirectory.Application.Reconcile;

public record VerifyDirectoryReconcileQuery : IRequest<DirectoryReconcileVerifyDto>;

public sealed class VerifyDirectoryReconcileQueryHandler(IDirectoryReconciliationService reconcile)
    : IRequestHandler<VerifyDirectoryReconcileQuery, DirectoryReconcileVerifyDto>
{
    public Task<DirectoryReconcileVerifyDto> Handle(VerifyDirectoryReconcileQuery request, CancellationToken ct) =>
        reconcile.VerifyAsync(ct);
}

public record ReconcileDirectoryCommand : IRequest<DirectoryReconcileReportDto>;

public sealed class ReconcileDirectoryCommandHandler(IDirectoryReconciliationService reconcile)
    : IRequestHandler<ReconcileDirectoryCommand, DirectoryReconcileReportDto>
{
    public Task<DirectoryReconcileReportDto> Handle(ReconcileDirectoryCommand request, CancellationToken ct) =>
        reconcile.ReconcileAsync(ct);
}

public record DeduplicateActiveNodeIncumbentsCommand(Guid? ChangedBy) : IRequest<int>;

public sealed class DeduplicateActiveNodeIncumbentsCommandHandler(IDirectoryWriteService write)
    : IRequestHandler<DeduplicateActiveNodeIncumbentsCommand, int>
{
    public Task<int> Handle(DeduplicateActiveNodeIncumbentsCommand request, CancellationToken ct) =>
        write.DeduplicateActiveNodeIncumbentsAsync(request.ChangedBy, ct);
}
