using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ParrainageBackend.Data;

public sealed class ParrainageDbContextFactory : IDesignTimeDbContextFactory<ParrainageDbContext>
{
    public ParrainageDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ParrainageDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5433;Database=parrainage_db;Username=parrainage_user;Password=Parrainage@2026")
            .Options;
        return new ParrainageDbContext(options);
    }
}
