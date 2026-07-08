using EmployeeDirectory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EmployeeDirectory.Infrastructure.Persistence;

/// <summary>
/// Département de production minimal pour l'import employés (colonne « OP-001 »).
/// </summary>
internal static class DockerComposeDirectoryDemoSeed
{
    internal static async Task ApplyIfEnabledAsync(IConfiguration configuration, DirectoryDbContext db, CancellationToken ct = default)
    {
        if (!string.Equals(configuration["KYNTUS_DIRECTORY_DEMO_SEED"], "true", StringComparison.OrdinalIgnoreCase))
            return;

        if (await db.BusinessDepartments.AnyAsync(d => d.Kind == BusinessDepartmentKind.Operational, ct))
            return;

        db.BusinessDepartments.Add(new BusinessDepartment
        {
            Id = Guid.Parse("11111111-1111-4111-8111-111111110001"),
            Code = "OP-001",
            Name = "Département de production",
            Kind = BusinessDepartmentKind.Operational,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(ct);
    }
}
