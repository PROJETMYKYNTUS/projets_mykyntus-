using System.Text.Json.Serialization;
using Kyntus.Identity.Jwt;
using Microsoft.EntityFrameworkCore;
using Parrainage.Application;
using Parrainage.Infrastructure;
using Parrainage.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
var isTesting = builder.Environment.IsEnvironment("Testing");

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

builder.Services.AddCors(options =>
{
    options.AddPolicy("devCors", policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddKyntusJwtAuthentication(builder.Configuration);
builder.Services.AddParrainageApplication();
builder.Services.AddParrainageInfrastructure(builder.Configuration, isTesting);

var app = builder.Build();

if (isTesting)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetService<ParrainageDbContext>();
    if (db is not null)
        await db.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("devCors");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    service = "parrainage-service",
    status = "running",
}));

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
