using EmployeeDirectory.Application.Abstractions;
using EmployeeDirectory.Infrastructure.Data;
using EmployeeDirectory.Infrastructure.Messaging;
using EmployeeDirectory.Infrastructure.Messaging.Consumers;
using EmployeeDirectory.Infrastructure.Services;
using Kyntus.Iam;
using Kyntus.Identity.Jwt;
using Kyntus.Messaging.Outbox;
using MassTransit;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.Services.AddCors(o => o.AddPolicy("devCors", p => p
    .WithOrigins("http://localhost:8200", "http://localhost:8201")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var conn = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=8433;Database=employee_directory_db;Username=directory_user;Password=Directory@2026";

builder.Services.AddDbContext<DirectoryDbContext>(o => o.UseNpgsql(conn));
builder.Services.AddKyntusOutbox<DirectoryDbContext>();
builder.Services.AddKyntusIam();

builder.Services.AddScoped<IDirectoryReadService, DirectoryReadService>();
builder.Services.AddScoped<IDirectoryWriteService, DirectoryWriteService>();
builder.Services.AddScoped<IDirectoryReconciliationService, DirectoryReconciliationService>();
builder.Services.AddHttpClient("DirectoryReconcile");
builder.Services.AddScoped<DirectoryHierarchyService>();
builder.Services.AddScoped<IIamReadService, DirectoryIamReadService>();
builder.Services.AddScoped<IPermissionCatalog, DirectoryIamReadService>();
builder.Services.AddScoped<IRebacClient, DirectoryIamReadService>();

builder.Services.AddScoped<DirectoryEmployeSyncService>();
builder.Services.AddScoped<DirectoryOrgSyncService>();
builder.Services.AddScoped<DirectoryAssignmentSyncService>();

builder.Services.AddKyntusJwtAuthentication(builder.Configuration);

var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<DirectoryEmployeSyncConsumer>();
    x.AddConsumer<DirectoryOrgSyncConsumer>();
    x.AddConsumer<DirectoryAssignmentSyncConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        cfg.ReceiveEndpoint("directory-employe-sync", e =>
        {
            e.ConfigureConsumer<DirectoryEmployeSyncConsumer>(ctx);
        });
        cfg.ReceiveEndpoint("directory-org-structure", e =>
        {
            e.ConfigureConsumer<DirectoryOrgSyncConsumer>(ctx);
        });
        cfg.ReceiveEndpoint("directory-org-assignment", e =>
        {
            e.ConfigureConsumer<DirectoryAssignmentSyncConsumer>(ctx);
        });
    });
});

var app = builder.Build();

await DirectoryDatabaseInitializer.InitializeAsync(app.Services);

app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(async () =>
    {
        var config = app.Services.GetRequiredService<IConfiguration>();
        if (!config.GetValue("Directory:EnablePrimeBootstrap", false))
            return;

        await Task.Delay(TimeSpan.FromSeconds(5));
        await DirectoryPrimeBootstrap.BootstrapFromPrimeIfNeededAsync(app.Services);
    });
});

app.UseCors("devCors");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
