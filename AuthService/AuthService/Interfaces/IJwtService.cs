using AuthService.Models;
using System.Security.Claims;

namespace AuthService.Interfaces
{
    public interface IJwtService
    {
        string GenerateAccessToken(User user);
        string GenerateRefreshToken();
        ClaimsPrincipal? ValidateToken(string token);
        int? GetUserIdFromToken(string token);
        IEnumerable<Claim> GetClaimsFromToken(string token);

    }
}