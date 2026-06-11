using System;
using System.Threading.Tasks;
using System.Text.Json;
using Formation.Domain.Exceptions;
using Microsoft.AspNetCore.Http;

namespace Formation.API.Middlewares;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    public ExceptionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await _next(ctx); }
        catch (Exception ex)
        {
            ctx.Response.ContentType = "application/json";
            ctx.Response.StatusCode = ex switch
            {
                FormationNotFoundException => 404,
                InvalidOperationException => 400,
                _ => 500
            };
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = ex.Message,
                statusCode = ctx.Response.StatusCode
            }));
        }
    }
}