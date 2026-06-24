using EmployeeDirectory.Domain.Entities;
using EmployeeDirectory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EmployeeDirectory.Infrastructure.Persistence;

public static class DirectoryDatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DirectoryDbContext>();
        var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DirectoryInit");

        const int maxRetries = 30;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await db.Database.EnsureCreatedAsync(ct);
                await DirectorySchemaPatches.ApplyAsync(db, ct);
                await SeedIamPermissionsAsync(db, ct);
                log.LogInformation("Employee Directory database ready.");
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                log.LogWarning(ex, "Waiting for directory DB... attempt {Attempt}/{MaxRetries}", attempt, maxRetries);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }

        throw new InvalidOperationException("Employee Directory database initialization failed after retries.");
    }

    private static async Task SeedIamPermissionsAsync(DirectoryDbContext db, CancellationToken ct)
    {
        if (await db.IamPermissions.AnyAsync(ct)) return;

        var seeds = new List<IamPermission>();
        void Add(string role, string action, string scope, bool allowed = true) =>
            seeds.Add(new IamPermission { Id = Guid.NewGuid(), Role = role, Action = action, Scope = scope, IsAllowed = allowed });

        foreach (var role in new[] { "Admin", "RH", "Audit" })
            Add(role, "*", "Global");

        foreach (var role in new[] { "Chef de projet", "RP" })
        {
            Add(role, "prime.fiche.view", "Pole");
            Add(role, "prime.fiche.validate", "Pole");
            Add(role, "planning.view", "Pole");
        }

        foreach (var role in new[] { "Superviseur", "Manager" })
        {
            Add(role, "prime.fiche.view", "Cellule");
            Add(role, "prime.fiche.validate", "Cellule");
            Add(role, "conge.approve", "Cellule");
            Add(role, "planning.edit", "Cellule");
        }

        foreach (var role in new[] { "Référent technique", "Coach" })
        {
            Add(role, "prime.fiche.view", "Service");
            Add(role, "formation.assign", "Service");
        }

        Add("Pilote", "conge.request", "Self");
        Add("Pilote", "prime.fiche.view", "Self");
        Add("Pilote", "documentation.request", "Self");
        Add("Pilote", "parrainage.view", "Self");

        db.IamPermissions.AddRange(seeds);
        await db.SaveChangesAsync(ct);
    }
}
