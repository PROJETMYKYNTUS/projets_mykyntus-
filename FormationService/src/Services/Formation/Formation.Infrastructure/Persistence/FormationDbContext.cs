using Formation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Formation.Infrastructure.Persistence;

public class FormationDbContext : DbContext
{
    public FormationDbContext(DbContextOptions<FormationDbContext> options) : base(options) { }

    public DbSet<FormationEntity> Formations => Set<FormationEntity>();
    public DbSet<Inscription> Inscriptions => Set<Inscription>();
    public DbSet<Certification> Certifications => Set<Certification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FormationEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Titre).IsRequired().HasMaxLength(200);
            e.Property(x => x.Prix).HasColumnType("decimal(18,2)");
            e.HasMany(x => x.Inscriptions).WithOne().HasForeignKey(i => i.FormationId);
        });

        modelBuilder.Entity<Inscription>(e =>
        {
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<Certification>(e =>
        {
            e.HasKey(x => x.Id);
        });
    }
}