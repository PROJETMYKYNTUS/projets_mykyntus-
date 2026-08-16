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

public record GetResponsiblesQuery(string Kind, string NodeId) : IRequest<IReadOnlyList<ResponsibleEmployeeDto>>;

public sealed class GetResponsiblesQueryHandler(IOrgResponsibilityResolver resolver)
    : IRequestHandler<GetResponsiblesQuery, IReadOnlyList<ResponsibleEmployeeDto>>
{
    public Task<IReadOnlyList<ResponsibleEmployeeDto>> Handle(GetResponsiblesQuery request, CancellationToken ct) =>
        resolver.GetResponsiblesAsync(request.Kind, request.NodeId, ct);
}

public record GetManagedEmployeesQuery(Guid EmployeeId) : IRequest<IReadOnlyList<string>>;

public sealed class GetManagedEmployeesQueryHandler(IOrgResponsibilityResolver resolver)
    : IRequestHandler<GetManagedEmployeesQuery, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(GetManagedEmployeesQuery request, CancellationToken ct)
    {
        var ids = await resolver.GetManagedEmployeeIdsAsync(request.EmployeeId, ct);
        return ids.Select(id => id.ToString()).ToList();
    }
}

public record CanActOnCommand(Guid ActorId, Guid TargetEmployeeId) : IRequest<CanActOnResultDto>;

public sealed class CanActOnCommandHandler(IOrgResponsibilityResolver resolver)
    : IRequestHandler<CanActOnCommand, CanActOnResultDto>
{
    public async Task<CanActOnResultDto> Handle(CanActOnCommand request, CancellationToken ct)
    {
        var allowed = await resolver.CanActOnAsync(request.ActorId, request.TargetEmployeeId, ct);
        return new CanActOnResultDto(allowed, request.ActorId.ToString(), request.TargetEmployeeId.ToString());
    }
}
