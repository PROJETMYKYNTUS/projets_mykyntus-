using Microsoft.EntityFrameworkCore;

namespace PrimeBackend.Data;

public class PrimeDbContext(DbContextOptions<PrimeDbContext> options) : DbContext(options)
{
    public DbSet<PoleEntity> Poles => Set<PoleEntity>();
    public DbSet<CelluleEntity> Cellules => Set<CelluleEntity>();
    public DbSet<ServiceEntity> Services => Set<ServiceEntity>();
    public DbSet<EmployeeEntity> Employees => Set<EmployeeEntity>();
    public DbSet<SupervisorPrimeFicheEntity> SupervisorPrimeFiches => Set<SupervisorPrimeFicheEntity>();
    public DbSet<ServicePrimeIndicatorEntity> ServicePrimeIndicators => Set<ServicePrimeIndicatorEntity>();
    public DbSet<SupervisorCellulePrimeDraftEntity> SupervisorCellulePrimeDrafts => Set<SupervisorCellulePrimeDraftEntity>();
    public DbSet<EmployeePrimeServiceFicheEntity> EmployeePrimeServiceFiches => Set<EmployeePrimeServiceFicheEntity>();
    // ---- Phase 1.3 : Administration ----
    public DbSet<RbacPermissionEntity> RbacPermissions => Set<RbacPermissionEntity>();
    public DbSet<WorkflowStepConfigEntity> WorkflowSteps => Set<WorkflowStepConfigEntity>();
    public DbSet<WorkflowGlobalConfigEntity> WorkflowGlobalConfigs => Set<WorkflowGlobalConfigEntity>();
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();
    public DbSet<AnomalyEntity> Anomalies => Set<AnomalyEntity>();
    public DbSet<GlobalPoolWorkflowStepEntity> GlobalPoolWorkflowSteps => Set<GlobalPoolWorkflowStepEntity>();
    public DbSet<GlobalPoolApprovalEntity> GlobalPoolApprovals => Set<GlobalPoolApprovalEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PoleEntity>(e =>
        {
            e.ToTable("prime_pole");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(256);
            e.HasMany(x => x.Cellules)
                .WithOne(x => x.Pole)
                .HasForeignKey(x => x.PoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CelluleEntity>(e =>
        {
            e.ToTable("prime_cellule");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(256);
            e.HasMany(x => x.Services)
                .WithOne(x => x.Cellule)
                .HasForeignKey(x => x.CelluleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ServiceEntity>(e =>
        {
            e.ToTable("prime_service");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(256);
        });

        modelBuilder.Entity<SupervisorPrimeFicheEntity>(e =>
        {
            e.ToTable("prime_supervisor_fiche");
            e.HasKey(x => x.Id);
            e.Property(x => x.SupervisorUserId).HasMaxLength(128);
            e.Property(x => x.CelluleId).HasMaxLength(128);
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
            e.HasIndex(x => x.ServiceId);
            e.HasIndex(x => x.PoleId);
            e.HasIndex(x => x.CelluleId);
            e.Property(x => x.CelluleId).IsRequired(false);
            e.Property(x => x.ServiceId).IsRequired(false);
            e.HasOne<ServiceEntity>()
                .WithMany()
                .HasForeignKey(x => x.ServiceId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ServicePrimeIndicatorEntity>(e =>
        {
            e.ToTable("prime_service_prime_indicator");
            e.HasKey(x => x.Id);
            e.Property(x => x.ServiceId).HasMaxLength(128);
            e.Property(x => x.Label).HasMaxLength(512);
            e.Property(x => x.PonderationPrimePct).HasPrecision(9, 4);
            e.Property(x => x.PonderationChallengePct).HasPrecision(9, 4);
            e.Property(x => x.TemplateStableId).HasMaxLength(256);
            e.HasIndex(x => x.ServiceId);
            e.HasIndex(x => new { x.ServiceId, x.SortOrder });
            e.HasOne(x => x.Service)
                .WithMany(x => x.PrimeIndicators)
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SupervisorCellulePrimeDraftEntity>(e =>
        {
            e.ToTable("prime_supervisor_cellule_prime_draft");
            e.HasKey(x => x.Id);
            e.Property(x => x.SupervisorUserId).HasMaxLength(128);
            e.Property(x => x.RootPoleId).HasMaxLength(128);
            e.Property(x => x.CelluleId).HasMaxLength(128);
            e.Property(x => x.Period).HasMaxLength(16);
            e.Property(x => x.TemplateId).HasMaxLength(128);
            e.Property(x => x.TemplateDisplayName).HasMaxLength(512);
            e.Property(x => x.Status).HasMaxLength(32);
            e.Property(x => x.GlobalPoolFileName).HasMaxLength(512);
            e.Property(x => x.GlobalPoolUploadedByUserId).HasMaxLength(128);
            e.Property(x => x.GlobalPoolManagerApprovedByUserId).HasMaxLength(128);
            e.Property(x => x.GlobalPoolRhApprovedByUserId).HasMaxLength(128);
            e.Property(x => x.GlobalPoolComptaAckByUserId).HasMaxLength(128);
            e.HasIndex(x => new { x.SupervisorUserId, x.Period });
            e.HasIndex(x => new { x.SupervisorUserId, x.RootPoleId, x.Period }).IsUnique();
            e.HasIndex(x => new { x.SupervisorUserId, x.CelluleId, x.Period, x.TemplateId });
            e.HasOne<PoleEntity>()
                .WithMany()
                .HasForeignKey(x => x.RootPoleId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.GlobalPoolApprovals)
                .WithOne(x => x.Draft)
                .HasForeignKey(x => x.DraftId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmployeePrimeServiceFicheEntity>(e =>
        {
            e.ToTable("prime_employee_prime_service_fiche");
            e.HasKey(x => x.Id);
            e.Property(x => x.SupervisorUserId).HasMaxLength(128);
            e.Property(x => x.EmployeeId).HasMaxLength(128);
            e.Property(x => x.ServiceId).HasMaxLength(128);
            e.Property(x => x.CelluleId).HasMaxLength(128);
            e.Property(x => x.Period).HasMaxLength(16);
            e.Property(x => x.FillingStatus).HasMaxLength(32);
            // ---- Phase 1.1 : workflow validation ----
            e.Property(x => x.ValidationStatus).HasMaxLength(64).IsRequired();
            e.Property(x => x.LastApproverUserId).HasMaxLength(128);
            e.Property(x => x.RejectedByUserId).HasMaxLength(128);
            e.Property(x => x.RejectionReason).HasMaxLength(2048);
            e.Property(x => x.PrimeAmount).HasPrecision(12, 2);
            e.Property(x => x.ChallengeAmount).HasPrecision(12, 2);
            e.Property(x => x.TotalAmount).HasPrecision(12, 2);
            e.HasIndex(x => new { x.ServiceId, x.Period });
            e.HasIndex(x => new { x.SupervisorUserId, x.Period });
            e.HasIndex(x => new { x.EmployeeId, x.Period }).IsUnique();
            e.HasIndex(x => x.ValidationStatus);
            e.HasOne(x => x.CellulePrimeDraft)
                .WithMany(x => x.EmployeeFiches)
                .HasForeignKey(x => x.CellulePrimeDraftId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Phase 1.3 : Administration ----
        modelBuilder.Entity<RbacPermissionEntity>(e =>
        {
            e.ToTable("prime_rbac_permission");
            e.HasKey(x => x.Id);
            e.Property(x => x.Role).HasMaxLength(64).IsRequired();
            e.Property(x => x.Action).HasMaxLength(32).IsRequired();
            e.Property(x => x.Scope).HasMaxLength(32).IsRequired();
            e.HasIndex(x => new { x.Role, x.Action, x.Scope }).IsUnique();
        });

        modelBuilder.Entity<WorkflowStepConfigEntity>(e =>
        {
            e.ToTable("prime_workflow_step");
            e.HasKey(x => x.Id);
            e.Property(x => x.ApproverRole).HasMaxLength(64).IsRequired();
            e.Property(x => x.FromStatus).HasMaxLength(64).IsRequired();
            e.Property(x => x.ToStatus).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.SortOrder);
            e.HasIndex(x => new { x.FromStatus, x.ApproverRole, x.ToStatus }).IsUnique();
        });

        modelBuilder.Entity<GlobalPoolWorkflowStepEntity>(e =>
        {
            e.ToTable("prime_global_pool_workflow_step");
            e.HasKey(x => x.Id);
            e.Property(x => x.ApproverRole).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.SortOrder);
        });

        modelBuilder.Entity<GlobalPoolApprovalEntity>(e =>
        {
            e.ToTable("prime_global_pool_approval");
            e.HasKey(x => x.Id);
            e.Property(x => x.UserId).HasMaxLength(128).IsRequired();
            e.HasIndex(x => new { x.DraftId, x.StepId }).IsUnique();
            e.HasOne(x => x.Step)
                .WithMany()
                .HasForeignKey(x => x.StepId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkflowGlobalConfigEntity>(e =>
        {
            e.ToTable("prime_workflow_global_config");
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<AuditLogEntity>(e =>
        {
            e.ToTable("prime_audit_log");
            e.HasKey(x => x.Id);
            e.Property(x => x.UserId).HasMaxLength(128);
            e.Property(x => x.UserDisplayName).HasMaxLength(256);
            e.Property(x => x.Role).HasMaxLength(64);
            e.Property(x => x.Action).HasMaxLength(64);
            e.Property(x => x.EntityType).HasMaxLength(128);
            e.Property(x => x.EntityId).HasMaxLength(128);
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.HasIndex(x => x.At);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
            e.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<AnomalyEntity>(e =>
        {
            e.ToTable("prime_anomaly");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(64).IsRequired();
            e.Property(x => x.Severity).HasMaxLength(16);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.Description).HasMaxLength(2048);
            e.Property(x => x.TargetEntityType).HasMaxLength(128);
            e.Property(x => x.TargetEntityId).HasMaxLength(128);
            e.Property(x => x.Period).HasMaxLength(16);
            e.Property(x => x.ServiceId).HasMaxLength(128);
            e.Property(x => x.CelluleId).HasMaxLength(128);
            e.Property(x => x.PoleId).HasMaxLength(128);
            e.Property(x => x.ResolvedByUserId).HasMaxLength(128);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.Type);
            e.HasIndex(x => new { x.TargetEntityType, x.TargetEntityId });
            e.HasIndex(x => new { x.Period, x.ServiceId });
        });
    }
}
