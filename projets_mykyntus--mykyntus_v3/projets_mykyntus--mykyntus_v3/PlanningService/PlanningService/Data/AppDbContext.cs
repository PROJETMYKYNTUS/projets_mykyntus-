using Microsoft.EntityFrameworkCore;
using PlanningService.Models;

namespace PlanningService.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // ── DbSets existants ──
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<Floor> Floors { get; set; } = null!;
    public DbSet<Service> Services { get; set; } = null!;
    public DbSet<Shift> Shifts { get; set; } = null!;
    public DbSet<ShiftAssignment> ShiftAssignments { get; set; } = null!;
    public DbSet<SubService> SubServices { get; set; } = null!;
    public DbSet<WeeklyPlanning> WeeklyPlannings { get; set; } = null!;
    public DbSet<WeeklyShiftConfig> WeeklyShiftConfigs { get; set; } = null!;
    public DbSet<Declaration> Declarations { get; set; } = null!;
    public DbSet<UserSubService> UserSubServices { get; set; } = null!;
    public DbSet<SaturdayGroup> SaturdayGroups { get; set; } = null!;
    public DbSet<Contract> Contracts { get; set; } = null!;
    public DbSet<ContractNotification> ContractNotifications { get; set; } = null!;
    public DbSet<Conge> Conges { get; set; } = null!;
    public DbSet<PlanningComment> PlanningComments { get; set; } = null!;
    public DbSet<SaturdayHistory> SaturdayHistories => Set<SaturdayHistory>();
    public DbSet<SubServiceShiftConfig> SubServiceShiftConfigs { get; set; } = null!;
    public DbSet<Reclamation> Reclamations { get; set; } = null!;
    public DbSet<ReclamationHistorique> ReclamationHistoriques { get; set; } = null!;
    public DbSet<Proposition> Propositions { get; set; } = null!;
    public DbSet<PropositionHistorique> PropositionHistoriques { get; set; } = null!;
    // ✅ NOUVEAU — Newsletter
    public DbSet<Newsletter> Newsletters { get; set; } = null!;
    public DbSet<NewsletterCampaign> NewsletterCampaigns { get; set; } = null!;
  
    public DbSet<CampaignAnalytics> CampaignAnalytics { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── User ──
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // ── Role ──
        modelBuilder.Entity<Role>()
            .HasIndex(r => r.Name)
            .IsUnique();

        // ── Service ──
        modelBuilder.Entity<Service>()
            .HasIndex(s => s.Code)
            .IsUnique();

        // ── SubService ──
        modelBuilder.Entity<SubService>()
            .HasIndex(s => s.Code)
            .IsUnique();

        // ── UserSubService ──
        modelBuilder.Entity<UserSubService>()
            .HasKey(us => new { us.UserId, us.SubServiceId });

        modelBuilder.Entity<UserSubService>()
            .HasOne(us => us.User)
            .WithMany(u => u.ManagedSubServices)
            .HasForeignKey(us => us.UserId);

        modelBuilder.Entity<UserSubService>()
            .HasOne(us => us.SubService)
            .WithMany(s => s.Managers)
            .HasForeignKey(us => us.SubServiceId);

        // ── ShiftAssignment ──
        modelBuilder.Entity<ShiftAssignment>()
            .HasIndex(sa => new { sa.UserId, sa.AssignedDate })
            .IsUnique();

        modelBuilder.Entity<ShiftAssignment>()
            .HasOne(a => a.User)
            .WithMany(u => u.ShiftAssignments)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ShiftAssignment>()
            .HasOne(a => a.WeeklyPlanning)
            .WithMany(p => p.ShiftAssignments)
            .HasForeignKey(a => a.WeeklyPlanningId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ShiftAssignment>()
            .HasOne(a => a.SubServiceShiftConfig)
            .WithMany()
            .HasForeignKey(a => a.SubServiceShiftConfigId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // ── WeeklyPlanning ──
        modelBuilder.Entity<WeeklyPlanning>()
            .HasOne(p => p.Validator)
            .WithMany()
            .HasForeignKey(p => p.ValidatedBy)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<WeeklyPlanning>()
            .HasIndex(p => new { p.WeekCode, p.SubServiceId })
            .IsUnique();

        // ── Declaration ──
        modelBuilder.Entity<Declaration>()
            .HasOne(d => d.User)
            .WithMany(u => u.Declarations)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Declaration>()
            .HasOne(d => d.Resolver)
            .WithMany()
            .HasForeignKey(d => d.ResolverId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Reclamation>().HasMany(r => r.Historique)
        .WithOne(h => h.Reclamation)
        .HasForeignKey(h => h.ReclamationId)
        .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Proposition>().HasMany(p => p.Historique)
            .WithOne(h => h.Proposition)
            .HasForeignKey(h => h.PropositionId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Conge ──
        modelBuilder.Entity<Conge>(entity =>
        {
            entity.HasOne(c => c.User)
                  .WithMany()
                  .HasForeignKey(c => c.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(c => new { c.UserId, c.StartDate, c.EndDate });
        });

        // ── SubServiceShiftConfig ──
        modelBuilder.Entity<SubServiceShiftConfig>(entity =>
        {
            entity.HasIndex(e => new { e.SubServiceId, e.WeekCode, e.Label }).IsUnique();
            entity.Property(e => e.Label).IsRequired().HasMaxLength(50);
            entity.Property(e => e.WeekCode).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Percentage).HasPrecision(5, 2);
            entity.Ignore(e => e.EndTime);
            entity.HasOne(e => e.SubService)
                  .WithMany()
                  .HasForeignKey(e => e.SubServiceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── PlanningComment ──
        modelBuilder.Entity<PlanningComment>(entity =>
        {
            entity.HasIndex(e => new { e.WeeklyPlanningId, e.UserId }).IsUnique();
            entity.Property(e => e.Comment).IsRequired().HasMaxLength(500);
            entity.HasOne(e => e.WeeklyPlanning)
                  .WithMany()
                  .HasForeignKey(e => e.WeeklyPlanningId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ✅ NOUVEAU — Newsletter ──────────────────────────────────────────────



        // ✅ CONFIGURATION CORRIGÉE — Newsletter
        modelBuilder.Entity<NewsletterCampaign>()
            .HasOne(c => c.Newsletter)
            .WithMany(n => n.Campaigns)
            .HasForeignKey(c => c.NewsletterId)
            .OnDelete(DeleteBehavior.Restrict);

        // ✅ À UTILISER
        modelBuilder.Entity<CampaignAnalytics>()
            .HasOne(a => a.Campaign)
            .WithMany(c => c.Analytics)
            .HasForeignKey(a => a.CampaignId);

        // On indique juste que UserId est requis, sans créer de relation complexe 
        // si ApplicationUser n'est pas géré par ce DbContext précis.
        modelBuilder.Entity<CampaignAnalytics>()
            .Property(a => a.UserId)
            .IsRequired();
    }
}