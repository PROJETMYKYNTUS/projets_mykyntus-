using Formation.Application.Commands.CreateFormation;
using Formation.Infrastructure.Messaging;
using Formation.Infrastructure.Persistence;
using Formation.Infrastructure.Repositories;
using Formation.Domain.Interfaces;
using Formation.API.Middlewares;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var isTesting = builder.Environment.IsEnvironment("Testing");

// DbContext
builder.Services.AddDbContext<FormationDbContext>(opt =>
{
    if (isTesting)
        opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
            ?? "DataSource=formation_characterization_test.db");
    else
        opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(CreateFormationCommand).Assembly));

// Repositories
builder.Services.AddScoped<IFormationRepository, FormationRepository>();
builder.Services.AddScoped<Formation.Infrastructure.Services.TrainingWorkflowService>();

if (isTesting)
{
    builder.Services.AddMassTransit(x => x.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx)));
}
else
{
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<FormationOrgAssignmentSyncConsumer>();
    x.AddConsumer<FormationDirectoryEmployeeProjectionConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        cfg.ReceiveEndpoint("formation-org-assignment", e =>
        {
            e.Bind("Kyntus.Messaging.Contracts:OrgAssignmentChangedMessage");
            e.ConfigureConsumer<FormationOrgAssignmentSyncConsumer>(ctx);
        });

        cfg.ReceiveEndpoint("formation-directory-projection", e =>
        {
            e.ConfigureConsumer<FormationDirectoryEmployeeProjectionConsumer>(ctx);
        });

        cfg.ConfigureEndpoints(ctx);
    });
});
}

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

// Migration automatique au démarrage
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FormationDbContext>();
    var startupLog = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Formation.Startup");
    if (isTesting)
        await db.Database.EnsureCreatedAsync();
    else
    {
        try
        {
            db.Database.Migrate();
        }
        catch (Exception ex)
        {
            startupLog.LogWarning(ex, "EF Migrate partiel — application des patches SQL training.");
        }

        try
        {
            await FormationSchemaPatches.EnsureTrainingWorkflowTablesAsync(db, startupLog);
        }
        catch (Exception ex)
        {
            startupLog.LogError(ex, "Impossible de créer les tables training workflow.");
            throw;
        }
    }

}

app.Run();

public partial class Program;
