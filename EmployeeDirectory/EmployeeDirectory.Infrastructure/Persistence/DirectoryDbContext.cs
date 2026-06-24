using EmployeeDirectory.Domain.Entities;
using EmployeeDirectory.Domain.Interfaces;
using Kyntus.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;

namespace EmployeeDirectory.Infrastructure.Persistence;

public class DirectoryDbContext(DbContextOptions<DirectoryDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<OrgPole> OrgPoles => Set<OrgPole>();
    public DbSet<OrgCellule> OrgCellules => Set<OrgCellule>();
    public DbSet<OrgService> OrgServices => Set<OrgService>();
    public DbSet<OrgAssignment> OrgAssignments => Set<OrgAssignment>();
    public DbSet<OrgAssignmentHistory> OrgAssignmentHistories => Set<OrgAssignmentHistory>();
    public DbSet<IamPermission> IamPermissions => Set<IamPermission>();
    public DbSet<BusinessDepartment> BusinessDepartments => Set<BusinessDepartment>();
    public DbSet<DepartmentPoleAssignment> DepartmentPoleAssignments => Set<DepartmentPoleAssignment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>(e =>
        {
            e.ToTable("employees");
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.FirstName).HasMaxLength(128);
            e.Property(x => x.LastName).HasMaxLength(128);
            e.Property(x => x.Role).HasMaxLength(64);
            e.Property(x => x.PoleId).HasMaxLength(64);
            e.Property(x => x.CelluleId).HasMaxLength(64);
            e.Property(x => x.ServiceId).HasMaxLength(64);
            e.Property(x => x.RowVersion)
                .HasColumnType("bytea")
                .IsConcurrencyToken()
                .ValueGeneratedNever();
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.BusinessDepartmentId);
            e.HasOne(x => x.BusinessDepartment).WithMany().HasForeignKey(x => x.BusinessDepartmentId);
        });

        modelBuilder.Entity<BusinessDepartment>(e =>
        {
            e.ToTable("business_departments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(256);
            e.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<DepartmentPoleAssignment>(e =>
        {
            e.ToTable("department_pole_assignments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.PoleId).HasMaxLength(64);
            e.HasIndex(x => new { x.BusinessDepartmentId, x.PoleId }).IsUnique();
            e.HasOne(x => x.BusinessDepartment).WithMany(x => x.PoleAssignments).HasForeignKey(x => x.BusinessDepartmentId);
        });

        modelBuilder.Entity<OrgPole>(e =>
        {
            e.ToTable("org_poles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(256);
            e.HasIndex(x => x.BusinessDepartmentId);
            e.HasOne(x => x.BusinessDepartment).WithMany().HasForeignKey(x => x.BusinessDepartmentId);
        });

        modelBuilder.Entity<OrgCellule>(e =>
        {
            e.ToTable("org_cellules");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.PoleId).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(256);
            e.HasOne(x => x.Pole).WithMany(x => x.Cellules).HasForeignKey(x => x.PoleId);
        });

        modelBuilder.Entity<OrgService>(e =>
        {
            e.ToTable("org_services");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.CelluleId).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(256);
            e.HasOne(x => x.Cellule).WithMany(x => x.Services).HasForeignKey(x => x.CelluleId);
        });

        modelBuilder.Entity<OrgAssignment>(e =>
        {
            e.ToTable("org_assignments");
            e.HasKey(x => x.Id);
            e.Property(x => x.NodeId).HasMaxLength(64);
            e.HasIndex(x => new { x.Kind, x.NodeId, x.EffectiveTo });
            e.HasIndex(x => x.EmployeeId);
        });

        modelBuilder.Entity<OrgAssignmentHistory>(e =>
        {
            e.ToTable("org_assignment_history");
            e.HasKey(x => x.Id);
            e.Property(x => x.NodeId).HasMaxLength(64);
            e.HasIndex(x => x.ChangedAt);
        });

        modelBuilder.Entity<IamPermission>(e =>
        {
            e.ToTable("iam_permissions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Role).HasMaxLength(64);
            e.Property(x => x.Action).HasMaxLength(128);
            e.Property(x => x.Scope).HasMaxLength(32);
            e.HasIndex(x => new { x.Role, x.Action, x.Scope }).IsUnique();
        });

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.ToTable("outbox_messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.MessageType).HasMaxLength(512);
            e.HasIndex(x => x.ProcessedAt);
        });
    }
}
