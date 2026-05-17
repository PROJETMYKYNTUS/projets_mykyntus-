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
                "http://localhost:4200",
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
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<PrimeAuditLogService>();
builder.Services.AddScoped<PrimeOrgScopeService>(sp =>
    new PrimeOrgScopeService(sp.GetService<PrimeDbContext>()));

var conn = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(conn))
{
    builder.Services.AddDbContext<PrimeDbContext>(o => o.UseNpgsql(conn));
    if (builder.Configuration.GetValue("Prime:ApplyMigrations", true))
        builder.Services.AddHostedService<PrimeDatabaseInitializer>();

    builder.Services.AddScoped<PrimeBackend.Services.AnomalyDetectionService>();
    builder.Services.AddScoped<PrimeValidationWorkflowRuntime>();
    builder.Services.AddScoped<PrimeRbacReadService>();
    builder.Services.AddScoped<IPrimeRequestUserResolver, PrimeRequestUserResolver>();
    builder.Services.AddScoped<GlobalPoolWorkflowService>();
}

var app = builder.Build();
app.UseCors("devCors");
app.UseMiddleware<ApiExceptionMiddleware>();
app.MapControllers();
app.Run();
