using Conge.Domain.Entities;
using Conge.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace Conge.Infrastructure.Persistence;

public class CongeDbContext : DbContext, IUnitOfWork
{
    public CongeDbContext(DbContextOptions<CongeDbContext> options) : base(options) { }

    public DbSet<DemandeConge> DemandeConges => Set<DemandeConge>();
    public DbSet<SoldeConge> SoldeConges => Set<SoldeConge>();
    public DbSet<EmployeSnapshot> EmployeSnapshots => Set<EmployeSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CongeDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
           => await base.SaveChangesAsync(cancellationToken);
}