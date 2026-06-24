namespace Prime.API.Middlewares;

using System.Net;

public class ApiExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var status = ex switch
            {
                KeyNotFoundException => HttpStatusCode.NotFound,
                UnauthorizedAccessException => HttpStatusCode.Forbidden,
                ArgumentException => HttpStatusCode.BadRequest,
                InvalidOperationException => HttpStatusCode.Conflict,
                _ => HttpStatusCode.InternalServerError
            };
            var message = ex.Message;
            if (ex is Microsoft.EntityFrameworkCore.DbUpdateException dbEx && dbEx.InnerException?.Message is { } inner)
                message = $"{dbEx.Message} — {inner}";

            context.Response.StatusCode = (int)status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                error = message,
                code = status.ToString()
            });
        }
    }
}
