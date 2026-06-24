using MediatR;
using Planning.Application.Abstractions;
using Planning.Application.DTOs;

namespace Planning.Application.Users;

public record GetManagersBySubServiceQuery(int SubServiceId) : IRequest<List<UserDto>>;

public sealed class GetManagersBySubServiceQueryHandler(IUserService users)
    : IRequestHandler<GetManagersBySubServiceQuery, List<UserDto>>
{
    public Task<List<UserDto>> Handle(GetManagersBySubServiceQuery request, CancellationToken ct) =>
        users.GetManagersBySubServiceAsync(request.SubServiceId, ct);
}

public record SetNewEmployeeStatusCommand(int Id, SetNewEmployeeDto Dto) : IRequest<SetNewEmployeeStatusResultDto?>;

public sealed class SetNewEmployeeStatusCommandHandler(IUserService users)
    : IRequestHandler<SetNewEmployeeStatusCommand, SetNewEmployeeStatusResultDto?>
{
    public Task<SetNewEmployeeStatusResultDto?> Handle(SetNewEmployeeStatusCommand request, CancellationToken ct) =>
        users.SetNewEmployeeStatusAsync(request.Id, request.Dto, ct);
}
