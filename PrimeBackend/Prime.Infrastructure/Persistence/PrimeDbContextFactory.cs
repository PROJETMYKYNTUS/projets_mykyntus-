using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Prime.Infrastructure.Persistence;

public sealed class PrimeDbContextFactory : IDesignTimeDbContextFactory<PrimeDbContext>
{
    public PrimeDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException(
                "Définir ConnectionStrings__DefaultConnection pour les migrations EF (design-time).");

        var options = new DbContextOptionsBuilder<PrimeDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new PrimeDbContext(options);
    }
}
