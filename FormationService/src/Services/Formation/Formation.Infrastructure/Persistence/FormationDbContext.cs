using Formation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Formation.Infrastructure.Persistence;

public class FormationDbContext : DbContext
{
    public FormationDbContext(DbContextOptions<FormationDbContext> options) : base(options) { }

    public DbSet<FormationEntity> Formations => Set<FormationEntity>();
    public DbSet<Inscription> Inscriptions => Set<Inscription>();
    public DbSet<Certification> Certifications => Set<Certification>();
    public DbSet<EmployeAnnuaire> EmployeAnnuaires => Set<EmployeAnnuaire>();
    public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();
    public DbSet<TrainingAssignment> TrainingAssignments => Set<TrainingAssignment>();
    public DbSet<InitialTrainingPath> InitialTrainingPaths => Set<InitialTrainingPath>();

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

        modelBuilder.Entity<EmployeAnnuaire>(e =>
        {
            e.ToTable("employe_annuaires");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.EmployeId).IsUnique();
            e.HasIndex(x => x.Email);
            e.Property(x => x.Nom).HasMaxLength(200);
            e.Property(x => x.Prenom).HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(320);
            e.Property(x => x.Role).HasMaxLength(100);
        });

        modelBuilder.Entity<TrainingSession>(e =>
        {
            e.ToTable("training_sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.HasMany(x => x.Assignments).WithOne(x => x.Session).HasForeignKey(x => x.SessionId);
        });

        modelBuilder.Entity<TrainingAssignment>(e =>
        {
            e.ToTable("training_assignments");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SessionId, x.EmployeeId }).IsUnique();
        });

        modelBuilder.Entity<InitialTrainingPath>(e =>
        {
            e.ToTable("initial_training_paths");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.EmployeeId);
        });
    }
}