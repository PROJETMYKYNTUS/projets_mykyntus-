using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Parrainage.Infrastructure.Persistence;

public sealed class ParrainageDbContextFactory : IDesignTimeDbContextFactory<ParrainageDbContext>
{
    public ParrainageDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException(
                "Définir ConnectionStrings__DefaultConnection pour les migrations EF (design-time).");

        var options = new DbContextOptionsBuilder<ParrainageDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new ParrainageDbContext(options);
    }
}
