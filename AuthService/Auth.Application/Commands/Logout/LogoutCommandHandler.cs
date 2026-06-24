using Auth.Domain.Interfaces;
using MediatR;

namespace Auth.Application.Commands.Logout;

public record LogoutCommand(string RefreshToken) : IRequest<bool>;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, bool>
{
    private readonly IUserRepository _userRepository;

    public LogoutCommandHandler(IUserRepository userRepository) => _userRepository = userRepository;

    public async Task<bool> Handle(LogoutCommand request, CancellationToken ct)
    {
        var users = await _userRepository.GetAllAsync(ct);
        var user = users.FirstOrDefault(u => u.RefreshToken == request.RefreshToken);
        if (user == null)
            return false;

        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        await _userRepository.UpdateAsync(user, ct);
        return true;
    }
}
