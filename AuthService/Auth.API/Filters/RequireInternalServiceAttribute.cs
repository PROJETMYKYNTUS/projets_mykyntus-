using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Auth.API.Filters;

/// <summary>
/// Exige le header X-Internal-Service-Key correspondant à InternalServices:ApiKey.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireInternalServiceAttribute : Attribute, IAuthorizationFilter
{
    public const string HeaderName = "X-Internal-Service-Key";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var configuration = context.HttpContext.RequestServices
            .GetRequiredService<IConfiguration>();
        var configuredKey = configuration["InternalServices:ApiKey"];

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            context.Result = new ObjectResult(new { message = "Clé service interne non configurée." })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable,
            };
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var provided)
            || !string.Equals(provided.ToString(), configuredKey, StringComparison.Ordinal))
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Clé service interne invalide." });
        }
    }
}
