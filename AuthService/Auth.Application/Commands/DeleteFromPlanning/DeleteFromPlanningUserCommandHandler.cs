using Auth.Domain.Interfaces;
using MediatR;

namespace Auth.Application.Commands.DeleteFromPlanning;

public record DeleteFromPlanningUserCommand(int AuthUserId) : IRequest<bool>;

public class DeleteFromPlanningUserCommandHandler : IRequestHandler<DeleteFromPlanningUserCommand, bool>
{
    private readonly IUserRepository _userRepository;

    public DeleteFromPlanningUserCommandHandler(IUserRepository userRepository) =>
        _userRepository = userRepository;

    public Task<bool> Handle(DeleteFromPlanningUserCommand request, CancellationToken ct) =>
        _userRepository.DeleteAsync(request.AuthUserId, ct);
}
