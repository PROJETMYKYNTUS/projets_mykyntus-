using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
        .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

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
    var migrated = false;
    var maxRetries = 10;
    for (var i = 0; i < maxRetries; i++)
    {
        try
        {
            db.Database.Migrate();
            Console.WriteLine("Auth migrations applied.");
            migrated = true;
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Waiting for DB... attempt {i + 1}/{maxRetries}: {ex.Message}");
            Thread.Sleep(3000);
        }
    }

    if (!migrated)
        Console.WriteLine("WARNING: EF migrations not applied after retries; running SubjectId schema repair.");

    try
    {
        EnsureSubjectIdColumn(db);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"WARNING: SubjectId schema repair skipped: {ex.Message}");
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
            new Role { Id = 8, Name = "Equipe formation", Description = "Équipe de formation", CreatedAt = DateTime.UtcNow },
            new Role { Id = 9, Name = "Superviseur", Description = "Superviseur de cellule PRIME", CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
        Console.WriteLine("Auth roles seeded.");
    }

    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

    if (!db.Users.Any())
    {
        db.Users.AddRange(
            SeedUser("Employee", "employee@kyntus.ma", "Employee@2026", 1, passwordHasher),
            SeedUser("rh", "rh@kyntus.ma", "RH@2026", 2, passwordHasher),
            SeedUser("manager", "manager@kyntus.ma", "Manager@2026", 3, passwordHasher),
            SeedUser("coach", "coach@kyntus.ma", "Coach@2026", 4, passwordHasher),
            SeedUser("rp", "rp@kyntus.ma", "RP@2026", 5, passwordHasher),
            SeedUser("admin", "admin@kyntus.ma", "Admin@2026", 6, passwordHasher),
            SeedUser("audit", "audit@kyntus.ma", "Audit@2026", 7, passwordHasher),
            SeedUser("equipeformation", "formation@kyntus.ma", "Formation@2026", 8, passwordHasher),
            SeedUser("superviseur", "superviseur@kyntus.ma", "Superviseur@2026", 9, passwordHasher));
        db.SaveChanges();
        Console.WriteLine("Auth users seeded.");
    }

    try
    {
        EnsureSuperviseurAccount(db, passwordHasher);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"WARNING: superviseur seed skipped: {ex.Message}");
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

static void EnsureSubjectIdColumn(AppDbContext db)
{
    db.Database.ExecuteSqlRaw("""
        DO $$
        BEGIN
          IF NOT EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'Users' AND column_name = 'SubjectId'
          ) THEN
            ALTER TABLE "Users" ADD COLUMN "SubjectId" uuid;
          END IF;
        END $$;

        UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111103'::uuid WHERE lower("Email") = lower('employee@kyntus.ma');
        UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111104'::uuid WHERE lower("Email") = lower('rh@kyntus.ma');
        UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111105'::uuid WHERE lower("Email") = lower('manager@kyntus.ma');
        UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111106'::uuid WHERE lower("Email") = lower('coach@kyntus.ma');
        UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111107'::uuid WHERE lower("Email") = lower('rp@kyntus.ma');
        UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111108'::uuid WHERE lower("Email") = lower('admin@kyntus.ma');
        UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111109'::uuid WHERE lower("Email") = lower('audit@kyntus.ma');
        UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111110'::uuid WHERE lower("Email") = lower('formation@kyntus.ma');
        UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111111'::uuid WHERE lower("Email") = lower('superviseur@kyntus.ma');
        UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111101'::uuid WHERE lower("Email") = lower('yasmine.elamrani@atlas-tech-demo.dev');
        UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111102'::uuid WHERE lower("Email") = lower('fatima.alaoui@atlas-tech-demo.dev');
        UPDATE "Users" SET "SubjectId" = gen_random_uuid() WHERE "SubjectId" IS NULL;

        ALTER TABLE "Users" ALTER COLUMN "SubjectId" SET NOT NULL;

        DROP INDEX IF EXISTS "IX_Users_SubjectId";
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_SubjectId" ON "Users" ("SubjectId");

        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
        SELECT '20260603120000_AddUserSubjectId', '10.0.0'
        WHERE NOT EXISTS (
          SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260603120000_AddUserSubjectId'
        );
        """);
    Console.WriteLine("Auth SubjectId schema ensured.");
}

static User SeedUser(string username, string email, string password, int roleId, IPasswordHasher hasher) =>
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

static void EnsureSuperviseurAccount(AppDbContext db, IPasswordHasher hasher)
{
    var role = db.Roles.FirstOrDefault(r => r.Name == "Superviseur");
    if (role == null)
    {
        var nextId = (db.Roles.Max(r => (int?)r.Id) ?? 0) + 1;
        role = new Role
        {
            Id = nextId,
            Name = "Superviseur",
            Description = "Superviseur de cellule PRIME",
            CreatedAt = DateTime.UtcNow,
        };
        db.Roles.Add(role);
        db.SaveChanges();
        Console.WriteLine("Auth role Superviseur added.");
    }

    if (db.Users.Any(u => u.Email.ToLower() == "superviseur@kyntus.ma"))
        return;

    db.Users.Add(new User
    {
        Username = "superviseur",
        Email = "superviseur@kyntus.ma",
        SubjectId = KyntusSubjectIdCatalog.ResolveForEmail("superviseur@kyntus.ma"),
        PasswordHash = hasher.HashPassword("Superviseur@2026"),
        RoleId = role.Id,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    });
    db.SaveChanges();
    Console.WriteLine("Auth user superviseur@kyntus.ma added.");
}
