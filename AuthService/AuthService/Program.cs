using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using AuthService.Data;
using AuthService.Interfaces;
using AuthService.Repositories;
using AuthService.Services;
using AuthService.Helpers;
using AuthService.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IAuthService, AuthenticationService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret non configurée");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };
});
builder.Services.AddHealthChecks();
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AuthService API",
        Version = "v1",
        Description = "API d'authentification avec JWT"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Exemple: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();
app.MapHealthChecks("/health");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var maxRetries = 10;
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            db.Database.Migrate();
            Console.WriteLine("Auth migrations applied.");
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Waiting for DB... attempt {i + 1}/{maxRetries}: {ex.Message}");
            Thread.Sleep(3000);
        }
    }

    if (!db.Roles.Any())
    {
        db.Roles.AddRange(
            new Role { Id = 1, Name = "Employee", Description = "Employee du système", CreatedAt = DateTime.UtcNow },
            new Role { Id = 2, Name = "RH", Description = "Responsable des ressources humaines", CreatedAt = DateTime.UtcNow },
            new Role { Id = 3, Name = "Manager", Description = "Manager de planning", CreatedAt = DateTime.UtcNow },
            new Role { Id = 4, Name = "Coach", Description = "Coach des équipes", CreatedAt = DateTime.UtcNow },
            new Role { Id = 5, Name = "RP", Description = "Responsable de production", CreatedAt = DateTime.UtcNow },
            new Role { Id = 6, Name = "Admin", Description = "Administrateur système", CreatedAt = DateTime.UtcNow },
            new Role { Id = 7, Name = "Audit", Description = "Auditeur interne", CreatedAt = DateTime.UtcNow },
            new Role { Id = 8, Name = "Equipe formation", Description = "Équipe de formation", CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
        Console.WriteLine("Auth roles seeded.");
    }

    if (!db.Users.Any())
    {
        var hasher = new PasswordHasher();
        db.Users.AddRange(
            SeedUser("Employee", "employee@kyntus.ma", "Employee@2026", 1, hasher),
            SeedUser("rh", "rh@kyntus.ma", "RH@2026", 2, hasher),
            SeedUser("manager", "manager@kyntus.ma", "Manager@2026", 3, hasher),
            SeedUser("coach", "coach@kyntus.ma", "Coach@2026", 4, hasher),
            SeedUser("rp", "rp@kyntus.ma", "RP@2026", 5, hasher),
            SeedUser("admin", "admin@kyntus.ma", "Admin@2026", 6, hasher),
            SeedUser("audit", "audit@kyntus.ma", "Audit@2026", 7, hasher),
            SeedUser("equipeformation", "formation@kyntus.ma", "Formation@2026", 8, hasher));
        db.SaveChanges();
        Console.WriteLine("Auth users seeded.");
    }

    foreach (var user in db.Users.Where(u => u.SubjectId == Guid.Empty).ToList())
        user.SubjectId = KyntusSubjectIdCatalog.ResolveForEmail(user.Email);

    db.SaveChanges();
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

static User SeedUser(string username, string email, string password, int roleId, PasswordHasher hasher) =>
    new()
    {
        Username = username,
        Email = email,
        SubjectId = KyntusSubjectIdCatalog.ResolveForEmail(email),
        PasswordHash = hasher.HashPassword(password),
        RoleId = roleId,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };
