using MediatR;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Domain.Entities;

namespace Prime.Application.Rp;

public record GetRpAssignedProjectIdsQuery(string RpUserId) : IRequest<List<string>>;

public sealed class GetRpAssignedProjectIdsQueryHandler(IPrimeRpAppService rp)
    : IRequestHandler<GetRpAssignedProjectIdsQuery, List<string>>
{
    public Task<List<string>> Handle(GetRpAssignedProjectIdsQuery request, CancellationToken ct) =>
        rp.GetAssignedProjectIdsAsync(request.RpUserId, ct);
}

public record GetRpDashboardStatsQuery(string RpUserId) : IRequest<ChefProjetDashboardStats>;

public sealed class GetRpDashboardStatsQueryHandler(IPrimeRpAppService rp)
    : IRequestHandler<GetRpDashboardStatsQuery, ChefProjetDashboardStats>
{
    public Task<ChefProjetDashboardStats> Handle(GetRpDashboardStatsQuery request, CancellationToken ct) =>
        rp.GetDashboardStatsAsync(request.RpUserId, ct);
}

public record GetRpTeamPerformanceQuery(string RpUserId) : IRequest<List<ChefProjetTeamMemberPerformance>>;

public sealed class GetRpTeamPerformanceQueryHandler(IPrimeRpAppService rp)
    : IRequestHandler<GetRpTeamPerformanceQuery, List<ChefProjetTeamMemberPerformance>>
{
    public Task<List<ChefProjetTeamMemberPerformance>> Handle(GetRpTeamPerformanceQuery request, CancellationToken ct) =>
        rp.GetTeamPerformanceByProjectAsync(request.RpUserId, ct);
}

public record GetRpManagerValidatedQuery(string RpUserId) : IRequest<List<ChefProjetValidationItem>>;

public sealed class GetRpManagerValidatedQueryHandler(IPrimeRpAppService rp)
    : IRequestHandler<GetRpManagerValidatedQuery, List<ChefProjetValidationItem>>
{
    public Task<List<ChefProjetValidationItem>> Handle(GetRpManagerValidatedQuery request, CancellationToken ct) =>
        rp.GetSuperviseurValidatedPrimesAsync(request.RpUserId, ct);
}

public record UpdateRpValidationStatusCommand(string Id, UpdateChefProjetValidationStatusRequest Body, string RpUserId)
    : IRequest<ChefProjetValidationItem>;

public sealed class UpdateRpValidationStatusCommandHandler(IPrimeRpAppService rp)
    : IRequestHandler<UpdateRpValidationStatusCommand, ChefProjetValidationItem>
{
    public Task<ChefProjetValidationItem> Handle(UpdateRpValidationStatusCommand request, CancellationToken ct) =>
        rp.UpdateValidationStatusAsync(request.Id, request.Body.Status, request.RpUserId, ct);
}
