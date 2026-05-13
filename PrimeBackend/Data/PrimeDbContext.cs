using Microsoft.EntityFrameworkCore;

namespace PrimeBackend.Data;

public class PrimeDbContext(DbContextOptions<PrimeDbContext> options) : DbContext(options)
{
    public DbSet<DepartmentEntity> Departments => Set<DepartmentEntity>();
    public DbSet<EmployeeEntity> Employees => Set<EmployeeEntity>();
    public DbSet<SupervisorPrimeFicheEntity> SupervisorPrimeFiches => Set<SupervisorPrimeFicheEntity>();
    public DbSet<CellulePrimeIndicatorEntity> CellulePrimeIndicators => Set<CellulePrimeIndicatorEntity>();
    public DbSet<SupervisorPolePrimeDraftEntity> SupervisorPolePrimeDrafts => Set<SupervisorPolePrimeDraftEntity>();
    public DbSet<EmployeePrimeCellFicheEntity> EmployeePrimeCellFiches => Set<EmployeePrimeCellFicheEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DepartmentEntity>(e =>
        {
            e.ToTable("prime_department");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(256);
            e.HasMany(x => x.Poles)
                .WithOne(x => x.Department)
                .HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PoleEntity>(e =>
        {
            e.ToTable("prime_pole");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(256);
            e.HasMany(x => x.Cells)
                .WithOne(x => x.Pole)
                .HasForeignKey(x => x.PoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CelluleEntity>(e =>
        {
            e.ToTable("prime_cellule");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(256);
            e.HasMany(x => x.Teams)
                .WithOne(x => x.Cellule)
                .HasForeignKey(x => x.CelluleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TeamEntity>(e =>
        {
            e.ToTable("prime_team");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(256);
        });

        modelBuilder.Entity<SupervisorPrimeFicheEntity>(e =>
        {
            e.ToTable("prime_supervisor_fiche");
            e.HasKey(x => x.Id);
            e.Property(x => x.SupervisorUserId).HasMaxLength(128);
            e.Property(x => x.PoleId).HasMaxLength(128);
            e.Property(x => x.Period).HasMaxLength(16);
            e.Property(x => x.TemplateId).HasMaxLength(128);
            e.Property(x => x.TemplateDisplayName).HasMaxLength(512);
            e.Property(x => x.Status).HasMaxLength(32);
            e.HasIndex(x => new { x.SupervisorUserId, x.Period });
        });

        modelBuilder.Entity<EmployeeEntity>(e =>
        {
            e.ToTable("prime_employee");
            e.HasKey(x => x.Id);
            e.Property(x => x.FirstName).HasMaxLength(128);
            e.Property(x => x.LastName).HasMaxLength(128);
            e.Property(x => x.Role).HasMaxLength(64);
            e.Property(x => x.Email).HasMaxLength(256);
            e.HasIndex(x => x.TeamId);
            e.HasIndex(x => x.PoleId);
            e.HasIndex(x => x.CelluleId);
            e.HasOne<TeamEntity>()
                .WithMany()
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CellulePrimeIndicatorEntity>(e =>
        {
            e.ToTable("prime_cellule_prime_indicator");
            e.HasKey(x => x.Id);
            e.Property(x => x.CelluleId).HasMaxLength(128);
            e.Property(x => x.Label).HasMaxLength(512);
            e.Property(x => x.PonderationPrimePct).HasPrecision(9, 4);
            e.Property(x => x.PonderationChallengePct).HasPrecision(9, 4);
            e.Property(x => x.TemplateStableId).HasMaxLength(256);
            e.HasIndex(x => x.CelluleId);
            e.HasIndex(x => new { x.CelluleId, x.SortOrder });
            e.HasOne(x => x.Cellule)
                .WithMany(x => x.PrimeIndicators)
                .HasForeignKey(x => x.CelluleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SupervisorPolePrimeDraftEntity>(e =>
        {
            e.ToTable("prime_supervisor_pole_prime_draft");
            e.HasKey(x => x.Id);
            e.Property(x => x.SupervisorUserId).HasMaxLength(128);
            e.Property(x => x.PoleId).HasMaxLength(128);
            e.Property(x => x.Period).HasMaxLength(16);
            e.Property(x => x.TemplateId).HasMaxLength(128);
            e.Property(x => x.TemplateDisplayName).HasMaxLength(512);
            e.Property(x => x.Status).HasMaxLength(32);
            e.HasIndex(x => new { x.SupervisorUserId, x.Period });
            e.HasIndex(x => new { x.SupervisorUserId, x.PoleId, x.Period, x.TemplateId }).IsUnique();
        });

        modelBuilder.Entity<EmployeePrimeCellFicheEntity>(e =>
        {
            e.ToTable("prime_employee_prime_cell_fiche");
            e.HasKey(x => x.Id);
            e.Property(x => x.SupervisorUserId).HasMaxLength(128);
            e.Property(x => x.EmployeeId).HasMaxLength(128);
            e.Property(x => x.CelluleId).HasMaxLength(128);
            e.Property(x => x.PoleId).HasMaxLength(128);
            e.Property(x => x.Period).HasMaxLength(16);
            e.Property(x => x.FillingStatus).HasMaxLength(32);
            e.HasIndex(x => new { x.CelluleId, x.Period });
            e.HasIndex(x => new { x.SupervisorUserId, x.Period });
            e.HasIndex(x => new { x.EmployeeId, x.Period }).IsUnique();
            e.HasOne(x => x.PolePrimeDraft)
                .WithMany(x => x.EmployeeFiches)
                .HasForeignKey(x => x.PolePrimeDraftId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
