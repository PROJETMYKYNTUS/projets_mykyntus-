using Formation.Application.Commands.CreateFormation;
using Formation.Domain.Entities;
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

// Migration automatique au d�marrage
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FormationDbContext>();
    db.Database.Migrate();

    if (string.Equals(app.Configuration["KYNTUS_FORMATION_DEMO_SEED"], "true", StringComparison.OrdinalIgnoreCase)
        && !await db.Formations.AnyAsync())
    {
        var demo = FormationEntity.Create(
            "Formation d'accueil (d�mo Docker)",
            "Jeu de donn�es ins�r� automatiquement apr�s git clone (KYNTUS_FORMATION_DEMO_SEED).",
            "Formateur d�mo",
            DateTime.UtcNow.AddDays(7),
            DateTime.UtcNow.AddDays(9),
            25,
            0);
        db.Formations.Add(demo);
        await db.SaveChangesAsync();
    }
}

app.Run();