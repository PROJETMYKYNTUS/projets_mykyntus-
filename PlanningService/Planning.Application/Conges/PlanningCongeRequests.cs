using MediatR;
using Planning.Application.Abstractions;
using Planning.Application.DTOs;

namespace Planning.Application.Conges;

public record GetCongesBySubServiceQuery(int SubServiceId, string? WeekStart)
    : IRequest<IReadOnlyList<PlanningCongeListItemDto>>;

public sealed class GetCongesBySubServiceQueryHandler(IPlanningCongeService conges)
    : IRequestHandler<GetCongesBySubServiceQuery, IReadOnlyList<PlanningCongeListItemDto>>
{
    public Task<IReadOnlyList<PlanningCongeListItemDto>> Handle(
        GetCongesBySubServiceQuery request,
        CancellationToken ct) =>
        conges.GetBySubServiceAsync(request.SubServiceId, request.WeekStart, ct);
}

public record GetNewEmployeesBySubServiceQuery(int SubServiceId)
    : IRequest<IReadOnlyList<PlanningNewEmployeeDto>>;

public sealed class GetNewEmployeesBySubServiceQueryHandler(IPlanningCongeService conges)
    : IRequestHandler<GetNewEmployeesBySubServiceQuery, IReadOnlyList<PlanningNewEmployeeDto>>
{
    public Task<IReadOnlyList<PlanningNewEmployeeDto>> Handle(
        GetNewEmployeesBySubServiceQuery request,
        CancellationToken ct) =>
        conges.GetNewEmployeesAsync(request.SubServiceId, ct);
}

public record CreatePlanningCongeCommand(CreateCongeDto Dto) : IRequest<PlanningCongeListItemDto>;

public sealed class CreatePlanningCongeCommandHandler(IPlanningCongeService conges)
    : IRequestHandler<CreatePlanningCongeCommand, PlanningCongeListItemDto>
{
    public Task<PlanningCongeListItemDto> Handle(CreatePlanningCongeCommand request, CancellationToken ct) =>
        conges.CreateAsync(request.Dto, ct);
}

public record DeletePlanningCongeCommand(int Id) : IRequest<bool>;

public sealed class DeletePlanningCongeCommandHandler(IPlanningCongeService conges)
    : IRequestHandler<DeletePlanningCongeCommand, bool>
{
    public Task<bool> Handle(DeletePlanningCongeCommand request, CancellationToken ct) =>
        conges.DeleteAsync(request.Id, ct);
}

public record SetSaturdaySlotCommand(SetSaturdaySlotDto Dto) : IRequest<SetSaturdaySlotResultDto>;

public sealed class SetSaturdaySlotCommandHandler(IPlanningCongeService conges)
    : IRequestHandler<SetSaturdaySlotCommand, SetSaturdaySlotResultDto>
{
    public Task<SetSaturdaySlotResultDto> Handle(SetSaturdaySlotCommand request, CancellationToken ct) =>
        conges.SetSaturdaySlotAsync(request.Dto, ct);
}

public record GetBulkAbsenceDaysCommand(BulkAbsenceDaysRequestDto Request)
    : IRequest<BulkAbsenceDaysResponseDto>;

public sealed class GetBulkAbsenceDaysCommandHandler(IPlanningCongeService conges)
    : IRequestHandler<GetBulkAbsenceDaysCommand, BulkAbsenceDaysResponseDto>
{
    public Task<BulkAbsenceDaysResponseDto> Handle(GetBulkAbsenceDaysCommand request, CancellationToken ct) =>
        conges.GetBulkAbsenceDaysAsync(request.Request, ct);
}
