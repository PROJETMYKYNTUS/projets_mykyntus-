using MediatR;
using Planning.Application.Abstractions;
using Planning.Application.DTOs;

namespace Planning.Application.Queries.Roles;

public record GetAllRolesQuery : IRequest<IReadOnlyList<RoleListItemDto>>;

public sealed class GetAllRolesQueryHandler(IRoleService roles)
    : IRequestHandler<GetAllRolesQuery, IReadOnlyList<RoleListItemDto>>
{
    public Task<IReadOnlyList<RoleListItemDto>> Handle(GetAllRolesQuery request, CancellationToken ct) =>
        roles.GetActiveRolesAsync(ct);
}
