using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Kyntus.Characterization.Common;

/// <summary>
/// Génère des JWT alignés sur JwtSettings des tests de caractérisation.
/// </summary>
public static class KyntusTestJwt
{
    public const string DefaultSecret = "TestSecretKeyForCharacterizationTests_Min32Chars!";
    public const string DefaultIssuer = "AuthService";
    public const string DefaultAudience = "AuthServiceClient";

    public static string CreateBearerToken(
        int authUserId = 1,
        string email = "rh@kyntus.ma",
        string role = "RH",
        Guid? subjectId = null,
        string? secret = null,
        string? issuer = null,
        string? audience = null)
    {
        var sub = subjectId ?? Guid.Parse("11111111-1111-4111-8111-111111111104");
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, authUserId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role),
            new("sub", sub.ToString()),
            new("email", email),
            new("role", role),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret ?? DefaultSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: issuer ?? DefaultIssuer,
            audience: audience ?? DefaultAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static void AuthorizeClient(HttpClient client, string? bearerToken = null)
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                bearerToken ?? CreateBearerToken());
    }
}
