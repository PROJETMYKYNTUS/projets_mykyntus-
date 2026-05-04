using Formation.Application.Commands.CreateFormation;
using Formation.Infrastructure.Persistence;
using Formation.Infrastructure.Repositories;
using Formation.Domain.Interfaces;
using Formation.API.Middlewares;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// DbContext
builder.Services.AddDbContext<FormationDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(CreateFormationCommand).Assembly));

// Repositories
builder.Services.AddScoped<IFormationRepository, FormationRepository>();

builder.Services.AddControllers();
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
    db.Database.Migrate();
}

app.Run();