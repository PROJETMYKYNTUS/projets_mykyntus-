using MediatR;
using Documentation.Application.Abstractions;
using Documentation.Application.Api;

namespace Documentation.Application.Directory;

public record ListDirectoryUsersQuery : IRequest<IReadOnlyList<DirectoryUserResponse>>;
public sealed class ListDirectoryUsersQueryHandler(IDirectoryQueryAppService directory)
    : IRequestHandler<ListDirectoryUsersQuery, IReadOnlyList<DirectoryUserResponse>>
{
    public Task<IReadOnlyList<DirectoryUserResponse>> Handle(ListDirectoryUsersQuery request, CancellationToken ct) =>
        directory.ListUsersAsync(ct);
}

public record GetDirectoryUserQuery(Guid Id) : IRequest<DirectoryUserResponse?>;
public sealed class GetDirectoryUserQueryHandler(IDirectoryQueryAppService directory)
    : IRequestHandler<GetDirectoryUserQuery, DirectoryUserResponse?>
{
    public Task<DirectoryUserResponse?> Handle(GetDirectoryUserQuery request, CancellationToken ct) =>
        directory.GetUserAsync(request.Id, ct);
}

public record GetOrganisationPolesQuery : IRequest<IReadOnlyList<OrganizationalUnitSummary>>;
public sealed class GetOrganisationPolesQueryHandler(IDirectoryQueryAppService directory)
    : IRequestHandler<GetOrganisationPolesQuery, IReadOnlyList<OrganizationalUnitSummary>>
{
    public Task<IReadOnlyList<OrganizationalUnitSummary>> Handle(GetOrganisationPolesQuery request, CancellationToken ct) =>
        directory.GetPolesAsync(ct);
}

public record GetOrganisationCellulesQuery(Guid PoleId) : IRequest<IReadOnlyList<OrganizationalUnitSummary>>;
public sealed class GetOrganisationCellulesQueryHandler(IDirectoryQueryAppService directory)
    : IRequestHandler<GetOrganisationCellulesQuery, IReadOnlyList<OrganizationalUnitSummary>>
{
    public Task<IReadOnlyList<OrganizationalUnitSummary>> Handle(GetOrganisationCellulesQuery request, CancellationToken ct) =>
        directory.GetCellulesByPoleAsync(request.PoleId, ct);
}

public record GetOrganisationDepartementsQuery(Guid CelluleId) : IRequest<IReadOnlyList<OrganizationalUnitSummary>>;
public sealed class GetOrganisationDepartementsQueryHandler(IDirectoryQueryAppService directory)
    : IRequestHandler<GetOrganisationDepartementsQuery, IReadOnlyList<OrganizationalUnitSummary>>
{
    public Task<IReadOnlyList<OrganizationalUnitSummary>> Handle(GetOrganisationDepartementsQuery request, CancellationToken ct) =>
        directory.GetDepartementsByCelluleAsync(request.CelluleId, ct);
}

public record GetUsersByRoleAndOrgQuery(string Role, Guid PoleId, Guid CelluleId, Guid DepartementId)
    : IRequest<IReadOnlyList<DirectoryUserResponse>>;
public sealed class GetUsersByRoleAndOrgQueryHandler(IDirectoryQueryAppService directory)
    : IRequestHandler<GetUsersByRoleAndOrgQuery, IReadOnlyList<DirectoryUserResponse>>
{
    public Task<IReadOnlyList<DirectoryUserResponse>> Handle(GetUsersByRoleAndOrgQuery request, CancellationToken ct) =>
        directory.GetUsersByRoleAndOrgAsync(request.Role, request.PoleId, request.CelluleId, request.DepartementId, ct);
}

public record GetManagersByDepartementQuery(Guid DepartementId) : IRequest<IReadOnlyList<DirectoryUserResponse>>;
public sealed class GetManagersByDepartementQueryHandler(IDirectoryQueryAppService directory)
    : IRequestHandler<GetManagersByDepartementQuery, IReadOnlyList<DirectoryUserResponse>>
{
    public Task<IReadOnlyList<DirectoryUserResponse>> Handle(GetManagersByDepartementQuery request, CancellationToken ct) =>
        directory.GetManagersByDepartementAsync(request.DepartementId, ct);
}

public record GetCoachesByManagerQuery(Guid ManagerId, Guid? DepartementId) : IRequest<IReadOnlyList<DirectoryUserResponse>>;
public sealed class GetCoachesByManagerQueryHandler(IDirectoryQueryAppService directory)
    : IRequestHandler<GetCoachesByManagerQuery, IReadOnlyList<DirectoryUserResponse>>
{
    public Task<IReadOnlyList<DirectoryUserResponse>> Handle(GetCoachesByManagerQuery request, CancellationToken ct) =>
        directory.GetCoachesByManagerAsync(request.ManagerId, request.DepartementId, ct);
}

public record GetPilotesByCoachQuery(Guid CoachId, Guid? DepartementId) : IRequest<IReadOnlyList<DirectoryUserResponse>>;
public sealed class GetPilotesByCoachQueryHandler(IDirectoryQueryAppService directory)
    : IRequestHandler<GetPilotesByCoachQuery, IReadOnlyList<DirectoryUserResponse>>
{
    public Task<IReadOnlyList<DirectoryUserResponse>> Handle(GetPilotesByCoachQuery request, CancellationToken ct) =>
        directory.GetPilotesByCoachAsync(request.CoachId, request.DepartementId, ct);
}
