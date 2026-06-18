using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Kyntus.Identity.Jwt;

public static class KyntusJwtExtensions
{
    public static IServiceCollection AddKyntusJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("JwtSettings");
        var secret = jwtSettings["Secret"]
            ?? throw new InvalidOperationException("JwtSettings:Secret manquant.");
        var issuer = jwtSettings["Issuer"] ?? "AuthService";
        var audience = jwtSettings["Audience"] ?? "AuthServiceClient";

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorization();
        return services;
    }
}

public static class KyntusClaimsPrincipalExtensions
{
    public static string? GetEmail(this ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
            return null;
        return principal.FindFirstValue(ClaimTypes.Email)?.Trim()
            ?? principal.FindFirstValue("email")?.Trim();
    }

    public static string? GetAuthRole(this ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
            return null;
        return principal.FindFirstValue(ClaimTypes.Role)?.Trim()
            ?? principal.FindFirstValue("role")?.Trim();
    }

    public static Guid? GetSubjectId(this ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
            return null;
        var sub = principal.FindFirstValue("sub")?.Trim();
        if (Guid.TryParse(sub, out var g) && g != Guid.Empty)
            return g;
        return null;
    }

    public static int? GetAuthUserId(this ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
            return null;
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }
}
