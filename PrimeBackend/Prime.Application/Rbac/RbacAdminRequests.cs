using MediatR;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;

namespace Prime.Application.Rbac;

public record ListRbacPermissionsQuery : IRequest<IReadOnlyList<RbacPermissionDto>>;
public sealed class ListRbacPermissionsQueryHandler(IRbacAdminService rbac)
    : IRequestHandler<ListRbacPermissionsQuery, IReadOnlyList<RbacPermissionDto>>
{
    public Task<IReadOnlyList<RbacPermissionDto>> Handle(ListRbacPermissionsQuery request, CancellationToken ct) =>
        rbac.ListAsync(ct);
}

public record GetRbacCatalogQuery : IRequest<RbacCatalogDto>;
public sealed class GetRbacCatalogQueryHandler(IRbacAdminService rbac)
    : IRequestHandler<GetRbacCatalogQuery, RbacCatalogDto>
{
    public Task<RbacCatalogDto> Handle(GetRbacCatalogQuery request, CancellationToken ct) =>
        rbac.GetCatalogAsync(ct);
}

public record UpsertRbacPermissionCommand(UpsertRbacPermissionRequest Body) : IRequest<RbacPermissionDto>;
public sealed class UpsertRbacPermissionCommandHandler(IRbacAdminService rbac)
    : IRequestHandler<UpsertRbacPermissionCommand, RbacPermissionDto>
{
    public Task<RbacPermissionDto> Handle(UpsertRbacPermissionCommand request, CancellationToken ct) =>
        rbac.UpsertAsync(request.Body, ct);
}

public record DeleteRbacPermissionCommand(Guid Id) : IRequest<bool>;
public sealed class DeleteRbacPermissionCommandHandler(IRbacAdminService rbac)
    : IRequestHandler<DeleteRbacPermissionCommand, bool>
{
    public Task<bool> Handle(DeleteRbacPermissionCommand request, CancellationToken ct) =>
        rbac.DeleteAsync(request.Id, ct);
}
