using Auth.Domain.Interfaces;
using MediatR;

namespace Auth.Application.Queries.CheckEmail;

public record CheckEmailQuery(string Email) : IRequest<bool>;

public class CheckEmailQueryHandler : IRequestHandler<CheckEmailQuery, bool>
{
    private readonly IUserRepository _userRepository;

    public CheckEmailQueryHandler(IUserRepository userRepository) => _userRepository = userRepository;

    public Task<bool> Handle(CheckEmailQuery request, CancellationToken ct) =>
        _userRepository.ExistsAsync(request.Email, ct);
}
