using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PrimeBackend.Data;

public sealed class PrimeDbContextFactory : IDesignTimeDbContextFactory<PrimeDbContext>
{
    public PrimeDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PrimeDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=prime_db;Username=prime_user;Password=Prime@2026")
            .Options;
        return new PrimeDbContext(options);
    }
}
