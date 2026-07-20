using Auth.Application.DTOs;
using Auth.Domain.Interfaces;
using MediatR;

namespace Auth.Application.Commands.Login;

public record LoginCommand(LoginDto Dto) : IRequest<AuthResponseDto>;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponseDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;
    private readonly IPasswordHasher _passwordHasher;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IJwtService jwtService,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
        _passwordHasher = passwordHasher;
    }

    public async Task<AuthResponseDto> Handle(LoginCommand request, CancellationToken ct)
    {
        var loginDto = request.Dto;
        var user = await _userRepository.GetByEmailAsync(loginDto.Email, ct)
            ?? throw new UnauthorizedAccessException("Email ou mot de passe incorrect");

        if (!_passwordHasher.VerifyPassword(user.PasswordHash, loginDto.Password))
            throw new UnauthorizedAccessException("Email ou mot de passe incorrect");

        user.RefreshToken = _jwtService.GenerateRefreshToken();
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _userRepository.UpdateAsync(user, ct);

        return AuthResponseMapper.ToDto(
            user,
            _jwtService.GenerateAccessToken(user),
            _jwtService.AccessTokenExpiresInSeconds);
    }
}
