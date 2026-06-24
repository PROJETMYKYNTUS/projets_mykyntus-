namespace EmployeeDirectory.API.Middlewares;

public class InternalServiceAuthMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private const string HeaderName = "X-Internal-Service-Key";

    public async Task InvokeAsync(HttpContext context)
    {
        var configuredKey = configuration["InternalServices:ApiKey"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            await next(context);
            return;
        }

        if (context.Request.Headers.TryGetValue(HeaderName, out var provided)
            && string.Equals(provided.ToString(), configuredKey, StringComparison.Ordinal))
        {
            context.Items["IsInternalService"] = true;
        }

        await next(context);
    }
}
