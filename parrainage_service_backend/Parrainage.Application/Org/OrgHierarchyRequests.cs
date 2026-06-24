using MediatR;
using Parrainage.Application.Abstractions;

namespace Parrainage.Application.Org;

public record ListOrgNodesQuery : IRequest<IReadOnlyList<OrgNodeDto>>;
public sealed class ListOrgNodesQueryHandler(IOrgHierarchyQueryService org)
    : IRequestHandler<ListOrgNodesQuery, IReadOnlyList<OrgNodeDto>>
{
    public Task<IReadOnlyList<OrgNodeDto>> Handle(ListOrgNodesQuery request, CancellationToken ct) =>
        org.ListNodesAsync(ct);
}
