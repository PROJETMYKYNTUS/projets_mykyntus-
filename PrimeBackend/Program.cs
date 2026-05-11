using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Infrastructure;
using PrimeBackend.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("devCors", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:4201",
                "http://localhost:4202",
                "http://localhost:4203",
                "http://localhost:4207")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddSingleton<PrimeInMemoryStore>();

var conn = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(conn))
{
    builder.Services.AddDbContext<PrimeDbContext>(o => o.UseNpgsql(conn));
    if (builder.Configuration.GetValue("Prime:ApplyMigrations", true))
        builder.Services.AddHostedService<PrimeDatabaseInitializer>();
}

var app = builder.Build();
app.UseCors("devCors");
app.UseMiddleware<ApiExceptionMiddleware>();
app.MapControllers();
app.Run();
