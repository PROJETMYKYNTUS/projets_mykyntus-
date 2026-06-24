using Auth.Application.DTOs;
using Auth.Domain.Entities;
using Auth.Domain.Interfaces;

namespace Auth.Application;

internal static class AuthResponseMapper
{
    public static AuthResponseDto ToDto(User user, string accessToken) => new()
    {
        AccessToken = accessToken,
        RefreshToken = user.RefreshToken ?? string.Empty,
        ExpiresIn = 900,
        TokenType = "Bearer",
        User = new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role?.Name ?? "Employee",
        },
    };
}
