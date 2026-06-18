using System.Text.Json.Serialization;
using MassTransit;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using ParrainageBackend.Data;
using Kyntus.Identity.Jwt;
using Kyntus.Iam;
using ParrainageBackend.Messaging;
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

builder.Services.AddKyntusJwtAuthentication(builder.Configuration);
builder.Services.AddKyntusIamViaDirectoryHttp(
    builder.Configuration["Directory:BaseUrl"] ?? "http://employee-directory-backend:8080");
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ParrainagePolicyService>();

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

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<EmployePortalSyncConsumer>();
    x.AddConsumer<OrgAssignmentPortalSyncConsumer>();
    x.AddConsumer<DirectoryEmployeePortalProjectionConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        cfg.ReceiveEndpoint("parrainage-employe-sync", e =>
        {
            e.ConfigureConsumer<EmployePortalSyncConsumer>(ctx);
            e.ConfigureConsumer<OrgAssignmentPortalSyncConsumer>(ctx);
        });

        cfg.ReceiveEndpoint("parrainage-directory-projection", e =>
        {
            e.ConfigureConsumer<DirectoryEmployeePortalProjectionConsumer>(ctx);
        });

        cfg.ConfigureEndpoints(ctx);
    });
});

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
