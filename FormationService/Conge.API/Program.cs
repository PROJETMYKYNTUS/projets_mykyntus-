using Conge.Application.Behaviors;
using Conge.Application.Contracts;
using Conge.Domain.Interfaces;
using Conge.Infrastructure.Messaging;
using Conge.Infrastructure.Messaging.Consumers;
using Conge.Infrastructure.Messaging.Publishers;
using Conge.Infrastructure.Persistence;
using Conge.Infrastructure.Persistence.Repositories;
using FluentValidation;
using Kyntus.Identity.Jwt;
using Kyntus.Iam;
using MassTransit;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var isTesting = builder.Environment.IsEnvironment("Testing");

// ?? Base de donn�es PostgreSQL ??????????????????????????????????????????????
builder.Services.AddDbContext<CongeDbContext>(options =>
{
    if (isTesting)
        options.UseSqlite(builder.Configuration.GetConnectionString("CongeDb")
            ?? "DataSource=conge_characterization_test.db");
    else
        options.UseNpgsql(builder.Configuration.GetConnectionString("CongeDb"));
});

// ?? Repositories ????????????????????????????????????????????????????????????
builder.Services.AddScoped<IDemandeCongeRepository, DemandeCongeRepository>();
builder.Services.AddScoped<ISoldeCongeRepository, SoldeCongeRepository>();
builder.Services.AddScoped<IEmployeSnapshotRepository, EmployeSnapshotRepository>();
builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CongeDbContext>());

// ?? MediatR + FluentValidation ???????????????????????????????????????????????
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(ValidationBehavior<,>).Assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssembly(typeof(DemanderCongeValidator).Assembly);

builder.Services.AddKyntusJwtAuthentication(builder.Configuration);
builder.Services.AddKyntusIamViaDirectoryHttp(
    builder.Configuration["Directory:BaseUrl"] ?? "http://employee-directory-backend:8080");

// ?? MassTransit + RabbitMQ ???????????????????????????????????????????????????
if (isTesting)
{
    builder.Services.AddMassTransit(x => x.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx)));
}
else
{
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<SoldeAnnuelInitialiseConsumer>();
    x.AddConsumer<OrgAssignmentCongeSyncConsumer>();
    x.AddConsumer<DirectoryEmployeeCongeProjectionConsumer>();
    x.AddConsumer<DirectoryEmployeeHrProfileCongeProjectionConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        cfg.ReceiveEndpoint("conge-solde-annuel", e =>
        {
            e.Bind("Kyntus.Messaging.Contracts:SoldeAnnuelInitialiseMessage");
            e.ConfigureConsumer<SoldeAnnuelInitialiseConsumer>(ctx);
        });

        cfg.ReceiveEndpoint("conge-org-assignment", e =>
        {
            e.Bind("Kyntus.Messaging.Contracts:OrgAssignmentChangedMessage");
            e.ConfigureConsumer<OrgAssignmentCongeSyncConsumer>(ctx);
        });

        cfg.ReceiveEndpoint("conge-directory-projection", e =>
        {
            e.ConfigureConsumer<DirectoryEmployeeCongeProjectionConsumer>(ctx);
        });

        cfg.ReceiveEndpoint("conge-directory-hr-profile", e =>
        {
            e.ConfigureConsumer<DirectoryEmployeeHrProfileCongeProjectionConsumer>(ctx);
        });

        cfg.ConfigureEndpoints(ctx);
    });
});
}

// ?? Publisher ????????????????????????????????????????????????????????????????
builder.Services.AddScoped<ICongeEventPublisher, CongeEventPublisher>();

// ?? CORS ?????????????????????????????????????????????????????????????????????
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// ?? API ???????????????????????????????????????????????????????????????????????
builder.Services.AddControllers(options =>
{
    var policy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter(policy));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Conge API", Version = "v1" });
});

var app = builder.Build();

// ?? Migrations automatiques au d�marrage ?????????????????????????????????????
if (isTesting)
{
    using var testScope = app.Services.CreateScope();
    var testDb = testScope.ServiceProvider.GetRequiredService<CongeDbContext>();
    await testDb.Database.EnsureCreatedAsync();
}
else
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CongeDbContext>();
    await db.Database.MigrateAsync();
}

// ?? Middleware pipeline ???????????????????????????????????????????????????????
app.UseMiddleware<Conge.API.Middlewares.ExceptionMiddleware>();

// ? CORS � doit �tre avant UseRouting et MapControllers
app.UseCors("AllowAll");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ? Swagger accessible aussi en Production pour les tests internes
if (app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Conge API v1"));
}

// ? HttpsRedirection d�sactiv� � cause des conflits derri�re Docker/Ocelot
// app.UseHttpsRedirection();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;