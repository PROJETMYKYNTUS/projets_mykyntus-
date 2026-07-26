using Auth.Domain.Entities;
using Auth.Domain.Interfaces;
using Auth.Infrastructure.Persistence;
using Auth.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.Infrastructure;

public static class AuthDatabaseInitializer
{
    public static void Initialize(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var migrated = false;
        const int maxRetries = 30;
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
        {
            Console.WriteLine("ERROR: EF migrations not applied after retries; skipping seed until next restart.");
            return;
        }

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
                new Role { Id = 8, Name = "Formateur", Description = "Équipe formation — parcours initiale", CreatedAt = DateTime.UtcNow },
                new Role { Id = 9, Name = "Superviseur", Description = "Superviseur de cellule PRIME", CreatedAt = DateTime.UtcNow },
                new Role { Id = 10, Name = "Qualiticien", Description = "Qualiticien — planification formation continue", CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
            Console.WriteLine("Auth roles seeded.");
        }

        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var subjectIdResolver = scope.ServiceProvider.GetRequiredService<ISubjectIdResolver>();

        if (!db.Users.Any())
        {
            if (!configuration.GetValue("DemoSeed:Enabled", false))
            {
                Console.WriteLine("DemoSeed désactivé — seed utilisateurs ignoré.");
            }
            else
            {
                SeedDemoUsers(db, passwordHasher, configuration, subjectIdResolver);
            }
        }
        else if (configuration.GetValue("DemoSeed:Enabled", false))
        {
            EnsureDemoUsers(db, passwordHasher, configuration, subjectIdResolver);
        }

        try
        {
            if (configuration.GetValue("DemoSeed:Enabled", false))
            {
                EnsureSuperviseurAccount(db, passwordHasher, configuration, subjectIdResolver);
                EnsureQualiticienAccount(db, passwordHasher, configuration, subjectIdResolver);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARNING: superviseur/qualiticien seed skipped: {ex.Message}");
        }

        try
        {
            RealignKnownSubjectIds(db, subjectIdResolver);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARNING: SubjectId realign skipped: {ex.Message}");
        }

        foreach (var user in db.Users.Where(u => u.SubjectId == Guid.Empty).ToList())
            user.SubjectId = subjectIdResolver.ResolveForEmail(user.Email);

        db.SaveChanges();
    }

    /// <summary>Réaligne SubjectId des emails connus (ex. formateur@gmail.com → Guid formateur seed).</summary>
    static void RealignKnownSubjectIds(AuthDbContext db, ISubjectIdResolver subjectIdResolver)
    {
        var emails = new[]
        {
            "employee@kyntus.ma", "rh@kyntus.ma", "manager@kyntus.ma", "coach@kyntus.ma",
            "rp@kyntus.ma", "admin@kyntus.ma", "audit@kyntus.ma", "formation@kyntus.ma",
            "formateur@gmail.com", "formateur@kyntus.ma", "superviseur@kyntus.ma",
            "qualiticien@kyntus.ma",
        };
        var changed = 0;
        foreach (var email in emails)
        {
            var expected = subjectIdResolver.ResolveForEmail(email);
            var user = db.Users.FirstOrDefault(u => u.Email.ToLower() == email);
            if (user is null || user.SubjectId == expected)
                continue;
            // Évite collision UNIQUE SubjectId
            if (db.Users.Any(u => u.Id != user.Id && u.SubjectId == expected))
                continue;
            user.SubjectId = expected;
            changed++;
        }

        if (changed > 0)
        {
            db.SaveChanges();
            Console.WriteLine($"Auth SubjectId realigned ({changed}).");
        }
    }

    static void EnsureSubjectIdColumn(AuthDbContext db)
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
            UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111120'::uuid WHERE lower("Email") = lower('formateur@gmail.com');
            UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111120'::uuid WHERE lower("Email") = lower('formateur@kyntus.ma');
            UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111111'::uuid WHERE lower("Email") = lower('superviseur@kyntus.ma');
            UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111101'::uuid WHERE lower("Email") = lower('yasmine.elamrani@atlas-tech-demo.dev');
            UPDATE "Users" SET "SubjectId" = '11111111-1111-4111-8111-111111111102'::uuid WHERE lower("Email") = lower('fatima.alaoui@atlas-tech-demo.dev');
            UPDATE "Users" SET "SubjectId" = gen_random_uuid() WHERE "SubjectId" IS NULL;

            ALTER TABLE "Users" ALTER COLUMN "SubjectId" SET NOT NULL;

            DROP INDEX IF EXISTS "IX_Users_SubjectId";
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_SubjectId" ON "Users" ("SubjectId");

            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20260603120000_AddUserSubjectId', '8.0.0'
            WHERE NOT EXISTS (
              SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260603120000_AddUserSubjectId'
            );
            """);
        Console.WriteLine("Auth SubjectId schema ensured.");
    }

    static void SeedDemoUsers(
        AuthDbContext db,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ISubjectIdResolver subjectIdResolver)
    {
        db.Users.AddRange(BuildDemoUsers(passwordHasher, configuration, subjectIdResolver));
        db.SaveChanges();
        Console.WriteLine("Auth users seeded.");
    }

    static void EnsureDemoUsers(
        AuthDbContext db,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ISubjectIdResolver subjectIdResolver)
    {
        var added = 0;
        foreach (var user in BuildDemoUsers(passwordHasher, configuration, subjectIdResolver))
        {
            if (db.Users.Any(u => u.Email.ToLower() == user.Email.ToLower()))
                continue;

            db.Users.Add(user);
            added++;
        }

        if (added > 0)
        {
            db.SaveChanges();
            Console.WriteLine($"Auth demo users ensured ({added} added).");
        }
    }

    static IEnumerable<User> BuildDemoUsers(
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ISubjectIdResolver subjectIdResolver) =>
    [
        SeedUser("Employee", "employee@kyntus.ma", DemoPassword(configuration, "Employee"), 1, passwordHasher, subjectIdResolver),
        SeedUser("rh", "rh@kyntus.ma", DemoPassword(configuration, "RH"), 2, passwordHasher, subjectIdResolver),
        SeedUser("manager", "manager@kyntus.ma", DemoPassword(configuration, "Manager"), 3, passwordHasher, subjectIdResolver),
        SeedUser("coach", "coach@kyntus.ma", DemoPassword(configuration, "Coach"), 4, passwordHasher, subjectIdResolver),
        SeedUser("rp", "rp@kyntus.ma", DemoPassword(configuration, "RP"), 5, passwordHasher, subjectIdResolver),
        SeedUser("admin", "admin@kyntus.ma", DemoPassword(configuration, "Admin"), 6, passwordHasher, subjectIdResolver),
        SeedUser("audit", "audit@kyntus.ma", DemoPassword(configuration, "Audit"), 7, passwordHasher, subjectIdResolver),
        SeedUser("equipeformation", "formation@kyntus.ma", DemoPassword(configuration, "Formation"), 8, passwordHasher, subjectIdResolver),
        SeedUser("formateur", "formateur@gmail.com", DemoPassword(configuration, "Formation"), 8, passwordHasher, subjectIdResolver),
        SeedUser("superviseur", "superviseur@kyntus.ma", DemoPassword(configuration, "Superviseur"), 9, passwordHasher, subjectIdResolver),
        SeedUser("qualiticien", "qualiticien@kyntus.ma", DemoPassword(configuration, "Qualiticien"), 10, passwordHasher, subjectIdResolver),
    ];

    static User SeedUser(
        string username,
        string email,
        string password,
        int roleId,
        IPasswordHasher hasher,
        ISubjectIdResolver subjectIdResolver) =>
        new()
        {
            Username = username,
            Email = email,
            SubjectId = subjectIdResolver.ResolveForEmail(email),
            PasswordHash = hasher.HashPassword(password),
            RoleId = roleId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

    static string DemoPassword(IConfiguration configuration, string roleKey)
    {
        var password = configuration[$"DemoSeed:Passwords:{roleKey}"];
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                $"DemoSeed:Passwords:{roleKey} est requis pour le seed démo (appsettings ou KYNTUS_DEMO_SEED_*).");
        return password;
    }

    static void EnsureSuperviseurAccount(
        AuthDbContext db,
        IPasswordHasher hasher,
        IConfiguration configuration,
        ISubjectIdResolver subjectIdResolver)
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
            SubjectId = subjectIdResolver.ResolveForEmail("superviseur@kyntus.ma"),
            PasswordHash = hasher.HashPassword(DemoPassword(configuration, "Superviseur")),
            RoleId = role.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        Console.WriteLine("Auth user superviseur@kyntus.ma added.");
    }

    static void EnsureQualiticienAccount(
        AuthDbContext db,
        IPasswordHasher hasher,
        IConfiguration configuration,
        ISubjectIdResolver subjectIdResolver)
    {
        var role = db.Roles.FirstOrDefault(r => r.Name == "Qualiticien");
        if (role == null)
        {
            var nextId = (db.Roles.Max(r => (int?)r.Id) ?? 0) + 1;
            role = new Role
            {
                Id = nextId,
                Name = "Qualiticien",
                Description = "Qualiticien — planification formation continue",
                CreatedAt = DateTime.UtcNow,
            };
            db.Roles.Add(role);
            db.SaveChanges();
            Console.WriteLine("Auth role Qualiticien added.");
        }

        if (db.Users.Any(u => u.Email.ToLower() == "qualiticien@kyntus.ma"))
            return;

        // Réutilise le mot de passe Formation si Qualiticien non défini (évite plantage seed).
        string password;
        try
        {
            password = DemoPassword(configuration, "Qualiticien");
        }
        catch
        {
            password = DemoPassword(configuration, "Formation");
        }

        db.Users.Add(new User
        {
            Username = "qualiticien",
            Email = "qualiticien@kyntus.ma",
            SubjectId = subjectIdResolver.ResolveForEmail("qualiticien@kyntus.ma"),
            PasswordHash = hasher.HashPassword(password),
            RoleId = role.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        Console.WriteLine("Auth user qualiticien@kyntus.ma added.");
    }
}
