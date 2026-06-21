using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ParrainageBackend.Data;

public sealed class ParrainageDbContextFactory : IDesignTimeDbContextFactory<ParrainageDbContext>
{
    public ParrainageDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=8433;Database=parrainage_db;Username=parrainage_user;Password=Parrainage@2026";

        var options = new DbContextOptionsBuilder<ParrainageDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new ParrainageDbContext(options);
    }
}
