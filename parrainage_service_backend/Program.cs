var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "parrainage-service",
    status = "running"
}));

app.MapGet("/api/parrainage/ping", () => Results.Ok(new
{
    message = "parrainage-service backend reachable"
}));

app.MapHealthChecks("/health");

app.Run();
