using Auth.Domain.Interfaces;
using MediatR;

namespace Auth.Application.Queries.CheckUsername;

public record CheckUsernameQuery(string Username) : IRequest<bool>;

public class CheckUsernameQueryHandler : IRequestHandler<CheckUsernameQuery, bool>
{
    private readonly IUserRepository _userRepository;

    public CheckUsernameQueryHandler(IUserRepository userRepository) => _userRepository = userRepository;

    public Task<bool> Handle(CheckUsernameQuery request, CancellationToken ct) =>
        _userRepository.UsernameExistsAsync(request.Username, ct);
}
