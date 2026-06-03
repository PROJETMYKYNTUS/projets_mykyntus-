using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using ParrainageBackend.Data;
using Kyntus.Identity.Jwt;
using ParrainageBackend.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 10 * 1024 * 1024);

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

var conn = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(conn))
{
    builder.Services.AddDbContext<ParrainageDbContext>(o => o.UseNpgsql(conn));
    builder.Services.AddScoped<ReferralRuleResolver>();
    builder.Services.AddScoped<ReferralWorkflowService>();
    builder.Services.AddScoped<ReferralEligibilityService>();
    builder.Services.AddSingleton<ReferralCvStorageService>();
    builder.Services.AddScoped<IParrainageRequestUserResolver, ParrainageRequestUserResolver>();
    builder.Services.AddHostedService<ParrainageDatabaseInitializer>();
    builder.Services.AddHostedService<ReferralEligibilityHostedService>();
}

var app = builder.Build();

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
    status = "running"
}));

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
