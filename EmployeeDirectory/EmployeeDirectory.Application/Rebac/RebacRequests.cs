using EmployeeDirectory.Application.Abstractions;
using EmployeeDirectory.Application.Dtos;
using MediatR;

namespace EmployeeDirectory.Application.Rebac;

public record IsDescendantQuery(Guid ViewerId, Guid TargetId) : IRequest<bool>;

public sealed class IsDescendantQueryHandler(IDirectoryReadService read)
    : IRequestHandler<IsDescendantQuery, bool>
{
    public Task<bool> Handle(IsDescendantQuery request, CancellationToken ct) =>
        read.IsDescendantAsync(request.ViewerId, request.TargetId, ct);
}

public record GetManagedNodesQuery(Guid EmployeeId, string Kind) : IRequest<RebacManagedNodesDto>;

public sealed class GetManagedNodesQueryHandler(IDirectoryReadService read)
    : IRequestHandler<GetManagedNodesQuery, RebacManagedNodesDto>
{
    public Task<RebacManagedNodesDto> Handle(GetManagedNodesQuery request, CancellationToken ct) =>
        read.GetManagedNodesAsync(request.EmployeeId, request.Kind, ct);
}

public record GetSubtreeQuery(Guid EmployeeId) : IRequest<RebacSubtreeDto>;

public sealed class GetSubtreeQueryHandler(IDirectoryReadService read)
    : IRequestHandler<GetSubtreeQuery, RebacSubtreeDto>
{
    public Task<RebacSubtreeDto> Handle(GetSubtreeQuery request, CancellationToken ct) =>
        read.GetSubtreeAsync(request.EmployeeId, ct);
}
