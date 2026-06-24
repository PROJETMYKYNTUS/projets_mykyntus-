using EmployeeDirectory.Application.Abstractions;
using EmployeeDirectory.Application.Dtos;
using MediatR;

namespace EmployeeDirectory.Application.BusinessDepartments;

public record ListBusinessDepartmentsQuery : IRequest<IReadOnlyList<BusinessDepartmentDto>>;

public sealed class ListBusinessDepartmentsQueryHandler(IDirectoryReadService read)
    : IRequestHandler<ListBusinessDepartmentsQuery, IReadOnlyList<BusinessDepartmentDto>>
{
    public Task<IReadOnlyList<BusinessDepartmentDto>> Handle(ListBusinessDepartmentsQuery request, CancellationToken ct) =>
        read.GetBusinessDepartmentsAsync(ct);
}

public record GetBusinessDepartmentByIdQuery(Guid Id) : IRequest<BusinessDepartmentDto?>;

public sealed class GetBusinessDepartmentByIdQueryHandler(IDirectoryReadService read)
    : IRequestHandler<GetBusinessDepartmentByIdQuery, BusinessDepartmentDto?>
{
    public Task<BusinessDepartmentDto?> Handle(GetBusinessDepartmentByIdQuery request, CancellationToken ct) =>
        read.GetBusinessDepartmentByIdAsync(request.Id, ct);
}

public record CreateBusinessDepartmentCommand(CreateBusinessDepartmentRequest Body) : IRequest<BusinessDepartmentDto>;

public sealed class CreateBusinessDepartmentCommandHandler(IDirectoryWriteService write)
    : IRequestHandler<CreateBusinessDepartmentCommand, BusinessDepartmentDto>
{
    public Task<BusinessDepartmentDto> Handle(CreateBusinessDepartmentCommand request, CancellationToken ct) =>
        write.CreateBusinessDepartmentAsync(request.Body, ct);
}

public record UpdateBusinessDepartmentCommand(Guid Id, UpdateBusinessDepartmentRequest Body) : IRequest<BusinessDepartmentDto?>;

public sealed class UpdateBusinessDepartmentCommandHandler(IDirectoryWriteService write)
    : IRequestHandler<UpdateBusinessDepartmentCommand, BusinessDepartmentDto?>
{
    public Task<BusinessDepartmentDto?> Handle(UpdateBusinessDepartmentCommand request, CancellationToken ct) =>
        write.UpdateBusinessDepartmentAsync(request.Id, request.Body, ct);
}

public record DeleteBusinessDepartmentCommand(Guid Id) : IRequest<bool>;

public sealed class DeleteBusinessDepartmentCommandHandler(IDirectoryWriteService write)
    : IRequestHandler<DeleteBusinessDepartmentCommand, bool>
{
    public Task<bool> Handle(DeleteBusinessDepartmentCommand request, CancellationToken ct) =>
        write.DeleteBusinessDepartmentAsync(request.Id, ct);
}

public record AssignPoleToBusinessDepartmentCommand(Guid DepartmentId, string PoleId) : IRequest<Unit>;

public sealed class AssignPoleToBusinessDepartmentCommandHandler(IDirectoryWriteService write)
    : IRequestHandler<AssignPoleToBusinessDepartmentCommand, Unit>
{
    public async Task<Unit> Handle(AssignPoleToBusinessDepartmentCommand request, CancellationToken ct)
    {
        await write.AssignPoleToBusinessDepartmentAsync(request.DepartmentId, request.PoleId, ct);
        return Unit.Value;
    }
}

public record RemovePoleFromBusinessDepartmentCommand(Guid DepartmentId, string PoleId) : IRequest<bool>;

public sealed class RemovePoleFromBusinessDepartmentCommandHandler(IDirectoryWriteService write)
    : IRequestHandler<RemovePoleFromBusinessDepartmentCommand, bool>
{
    public Task<bool> Handle(RemovePoleFromBusinessDepartmentCommand request, CancellationToken ct) =>
        write.RemovePoleFromBusinessDepartmentAsync(request.DepartmentId, request.PoleId, ct);
}

public record SetBusinessDepartmentManagerCommand(Guid DepartmentId, Guid EmployeeId, Guid? ChangedBy)
    : IRequest<StructuralRoleAssignmentResult>;

public sealed class SetBusinessDepartmentManagerCommandHandler(IDirectoryWriteService write)
    : IRequestHandler<SetBusinessDepartmentManagerCommand, StructuralRoleAssignmentResult>
{
    public Task<StructuralRoleAssignmentResult> Handle(SetBusinessDepartmentManagerCommand request, CancellationToken ct) =>
        write.SetBusinessDepartmentManagerAsync(request.DepartmentId, request.EmployeeId, request.ChangedBy, null, ct);
}

public record ClearBusinessDepartmentManagerCommand(Guid DepartmentId) : IRequest<bool>;

public sealed class ClearBusinessDepartmentManagerCommandHandler(IDirectoryWriteService write)
    : IRequestHandler<ClearBusinessDepartmentManagerCommand, bool>
{
    public Task<bool> Handle(ClearBusinessDepartmentManagerCommand request, CancellationToken ct) =>
        write.ClearBusinessDepartmentManagerAsync(request.DepartmentId, ct);
}
