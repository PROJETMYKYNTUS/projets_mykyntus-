using MediatR;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;

namespace Prime.Application.Org;

public record GetCelluleCommonLinePonderationsQuery(
    string CelluleId,
    string SupervisorUserId,
    string? TemplateId,
    DateTimeOffset? EffectiveAt) : IRequest<IReadOnlyList<EffectiveCommonLinePonderationDto>>;

public sealed class GetCelluleCommonLinePonderationsQueryHandler(ICommonLinePonderationsAppService ponderations)
    : IRequestHandler<GetCelluleCommonLinePonderationsQuery, IReadOnlyList<EffectiveCommonLinePonderationDto>>
{
    public Task<IReadOnlyList<EffectiveCommonLinePonderationDto>> Handle(
        GetCelluleCommonLinePonderationsQuery request, CancellationToken ct) =>
        ponderations.GetCelluleEffectiveAsync(
            request.CelluleId, request.SupervisorUserId, request.TemplateId, request.EffectiveAt, null, ct);
}

public record PutCelluleCommonLinePonderationsCommand(
    string CelluleId,
    string SupervisorUserId,
    PutCommonLinePonderationsRequest Body) : IRequest<IReadOnlyList<CommonLinePonderationDto>>;

public sealed class PutCelluleCommonLinePonderationsCommandHandler(ICommonLinePonderationsAppService ponderations)
    : IRequestHandler<PutCelluleCommonLinePonderationsCommand, IReadOnlyList<CommonLinePonderationDto>>
{
    public Task<IReadOnlyList<CommonLinePonderationDto>> Handle(
        PutCelluleCommonLinePonderationsCommand request, CancellationToken ct) =>
        ponderations.PutCelluleAsync(request.CelluleId, request.SupervisorUserId, request.Body, ct);
}

public record ConsolidateCelluleCommonLinePonderationsCommand(
    string CelluleId,
    string SupervisorUserId,
    string? TemplateId,
    DateTimeOffset? EffectiveAt) : IRequest<int>;

public sealed class ConsolidateCelluleCommonLinePonderationsCommandHandler(ICommonLinePonderationsAppService ponderations)
    : IRequestHandler<ConsolidateCelluleCommonLinePonderationsCommand, int>
{
    public Task<int> Handle(ConsolidateCelluleCommonLinePonderationsCommand request, CancellationToken ct) =>
        ponderations.ConsolidateIdenticalServiceOverridesAsync(
            request.CelluleId, request.SupervisorUserId, request.TemplateId, request.EffectiveAt, ct);
}

public record GetServiceCommonLinePonderationsQuery(
    string ServiceId,
    string SupervisorUserId,
    string? TemplateId,
    DateTimeOffset? EffectiveAt) : IRequest<IReadOnlyList<EffectiveCommonLinePonderationDto>>;

public sealed class GetServiceCommonLinePonderationsQueryHandler(ICommonLinePonderationsAppService ponderations)
    : IRequestHandler<GetServiceCommonLinePonderationsQuery, IReadOnlyList<EffectiveCommonLinePonderationDto>>
{
    public Task<IReadOnlyList<EffectiveCommonLinePonderationDto>> Handle(
        GetServiceCommonLinePonderationsQuery request, CancellationToken ct) =>
        ponderations.GetServiceEffectiveAsync(
            request.ServiceId, request.SupervisorUserId, request.TemplateId, request.EffectiveAt, null, ct);
}

public record PutServiceCommonLinePonderationsCommand(
    string ServiceId,
    string SupervisorUserId,
    PutCommonLinePonderationsRequest Body) : IRequest<IReadOnlyList<CommonLinePonderationDto>>;

public sealed class PutServiceCommonLinePonderationsCommandHandler(ICommonLinePonderationsAppService ponderations)
    : IRequestHandler<PutServiceCommonLinePonderationsCommand, IReadOnlyList<CommonLinePonderationDto>>
{
    public Task<IReadOnlyList<CommonLinePonderationDto>> Handle(
        PutServiceCommonLinePonderationsCommand request, CancellationToken ct) =>
        ponderations.PutServiceAsync(request.ServiceId, request.SupervisorUserId, request.Body, false, ct);
}

public record DeleteServiceCommonLinePonderationCommand(
    string ServiceId,
    string TemplateStableId,
    string SupervisorUserId,
    string? TemplateId,
    DateTimeOffset? EffectiveAt) : IRequest<Unit>;

public sealed class DeleteServiceCommonLinePonderationCommandHandler(ICommonLinePonderationsAppService ponderations)
    : IRequestHandler<DeleteServiceCommonLinePonderationCommand, Unit>
{
    public async Task<Unit> Handle(DeleteServiceCommonLinePonderationCommand request, CancellationToken ct)
    {
        await ponderations.DeleteServiceOverrideAsync(
            request.ServiceId,
            request.TemplateStableId,
            request.SupervisorUserId,
            request.TemplateId,
            request.EffectiveAt,
            ct);
        return Unit.Value;
    }
}
