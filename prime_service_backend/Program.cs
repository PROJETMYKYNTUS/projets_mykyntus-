var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "prime-service",
    status = "running"
}));

app.MapGet("/api/prime/ping", () => Results.Ok(new
{
    message = "prime-service backend reachable"
}));

app.MapHealthChecks("/health");

app.Run();
