using Auth.Domain.Entities;
using System.Security.Claims;

namespace Auth.Domain.Interfaces;

public interface IJwtService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateToken(string token);
    /// <summary>Durée de vie du access token en secondes (alignée sur le JWT émis).</summary>
    int AccessTokenExpiresInSeconds { get; }
}
