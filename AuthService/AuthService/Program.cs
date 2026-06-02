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
var secretKey = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret non configur�e");

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
// ? �TAPE 1 � Migrations
// ? �TAPE 1 � Migrations + Seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var maxRetries = 10;
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            db.Database.Migrate();
            Console.WriteLine("? Auth migrations appliqu�es avec succ�s.");
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Attente DB... tentative {i + 1}/{maxRetries}: {ex.Message}");
            Thread.Sleep(3000);
        }
    }

    // ? SEED � Roles
    if (!db.Roles.Any())
    {
        db.Roles.AddRange(
            new AuthService.Models.Role { Id = 1, Name = "Employee", Description = "Employee du syst�me", CreatedAt = DateTime.UtcNow },
            new AuthService.Models.Role { Id = 2, Name = "RH", Description = "Responsable des ressources humaines", CreatedAt = DateTime.UtcNow },
            new AuthService.Models.Role { Id = 3, Name = "Manager", Description = "Manager de planning", CreatedAt = DateTime.UtcNow },
            new AuthService.Models.Role { Id = 4, Name = "Coach", Description = "Coach des �quipes", CreatedAt = DateTime.UtcNow },
            new AuthService.Models.Role { Id = 5, Name = "RP", Description = "Responsable de production", CreatedAt = DateTime.UtcNow },
            new AuthService.Models.Role { Id = 6, Name = "Admin", Description = "Administrateur syst�me", CreatedAt = DateTime.UtcNow },
            new AuthService.Models.Role { Id = 7, Name = "Audit", Description = "Auditeur interne", CreatedAt = DateTime.UtcNow },
            new AuthService.Models.Role { Id = 8, Name = "Equipe formation", Description = "�quipe de formation", CreatedAt = DateTime.UtcNow }
            
           
        );
        
        db.SaveChanges();
        Console.WriteLine("? Roles ins�r�s.");
    }

    // ? SEED � Users
    if (!db.Users.Any())
    {
        var hasher = new AuthService.Helpers.PasswordHasher();
        db.Users.AddRange(
            new AuthService.Models.User { Username = "Employee", Email = "employee@kyntus.ma", PasswordHash = hasher.HashPassword("Employee@2026"), RoleId = 1, IsActive = true, CreatedAt = DateTime.UtcNow },
            new AuthService.Models.User { Username = "rh", Email = "rh@kyntus.ma", PasswordHash = hasher.HashPassword("RH@2026"), RoleId = 2, IsActive = true, CreatedAt = DateTime.UtcNow },
            new AuthService.Models.User { Username = "manager", Email = "manager@kyntus.ma", PasswordHash = hasher.HashPassword("Manager@2026"), RoleId = 3, IsActive = true, CreatedAt = DateTime.UtcNow },
            new AuthService.Models.User { Username = "coach", Email = "coach@kyntus.ma", PasswordHash = hasher.HashPassword("Coach@2026"), RoleId = 4, IsActive = true, CreatedAt = DateTime.UtcNow },
            new AuthService.Models.User { Username = "rp", Email = "rp@kyntus.ma", PasswordHash = hasher.HashPassword("RP@2026"), RoleId = 5, IsActive = true, CreatedAt = DateTime.UtcNow },
            new AuthService.Models.User { Username = "admin", Email = "admin@kyntus.ma", PasswordHash = hasher.HashPassword("Admin@2026"), RoleId = 6, IsActive = true, CreatedAt = DateTime.UtcNow },
            new AuthService.Models.User { Username = "audit", Email = "audit@kyntus.ma", PasswordHash = hasher.HashPassword("Audit@2026"), RoleId = 7, IsActive = true, CreatedAt = DateTime.UtcNow },
            new AuthService.Models.User { Username = "equipeformation", Email = "formation@kyntus.ma", PasswordHash = hasher.HashPassword("Formation@2026"), RoleId = 8, IsActive = true, CreatedAt = DateTime.UtcNow }
        );
        db.SaveChanges();
        Console.WriteLine("? Users ins�r�s.");
    }
}

// ? �TAPE 2 � Middleware
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ? �TAPE 3 � Run (une seule fois)
app.Run();