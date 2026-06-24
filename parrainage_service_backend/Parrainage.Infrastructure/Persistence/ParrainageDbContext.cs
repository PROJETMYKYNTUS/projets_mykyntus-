using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Parrainage.Domain.Entities;

namespace Parrainage.Infrastructure.Persistence;

public class ParrainageDbContext(DbContextOptions<ParrainageDbContext> options) : DbContext(options)
{
    public DbSet<ReferralEntity> Referrals => Set<ReferralEntity>();
    public DbSet<ReferralHistoryEntryEntity> ReferralHistory => Set<ReferralHistoryEntryEntity>();
    public DbSet<ReferralRuleEntity> ReferralRules => Set<ReferralRuleEntity>();
    public DbSet<ReferralNotificationEntity> ReferralNotifications => Set<ReferralNotificationEntity>();
    public DbSet<NotificationPreferenceEntity> NotificationPreferences => Set<NotificationPreferenceEntity>();
    public DbSet<SystemConfigEntity> SystemConfigs => Set<SystemConfigEntity>();
    public DbSet<AuditLogEntryEntity> AuditLogs => Set<AuditLogEntryEntity>();
    public DbSet<ParrainagePortalUserEntity> PortalUsers => Set<ParrainagePortalUserEntity>();

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var stringListConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, JsonOpts),
            v => string.IsNullOrWhiteSpace(v)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(v, JsonOpts) ?? new List<string>());

        var stringListComparer = new ValueComparer<List<string>>(
            (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
            v => v == null ? 0 : v.Aggregate(0, (acc, s) => HashCode.Combine(acc, s.GetHashCode())),
            v => v == null ? new List<string>() : v.ToList());

        modelBuilder.Entity<ParrainagePortalUserEntity>(e =>
        {
            e.ToTable("parrainage_portal_user");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(128);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Name).HasMaxLength(256);
            e.Property(x => x.Role).HasMaxLength(32);
            e.Property(x => x.ProjectId).HasMaxLength(128);
            e.Property(x => x.ParentId).HasMaxLength(128);
            e.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<ReferralEntity>(e =>
        {
            e.ToTable("parrainage_referral");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(128);
            e.Property(x => x.ReferrerId).HasMaxLength(128);
            e.Property(x => x.ReferrerName).HasMaxLength(256);
            e.Property(x => x.ProjectId).HasMaxLength(128);
            e.Property(x => x.ProjectName).HasMaxLength(256);
            e.Property(x => x.TeamId).HasMaxLength(128);
            e.Property(x => x.CandidateName).HasMaxLength(256);
            e.Property(x => x.CandidateEmail).HasMaxLength(256);
            e.Property(x => x.CandidatePhone).HasMaxLength(64);
            e.Property(x => x.Position).HasMaxLength(256);
            e.Property(x => x.PositionMode).HasMaxLength(16).HasDefaultValue(ReferralPositionMode.Custom);
            e.Property(x => x.AppliedRuleId).HasMaxLength(128);
            e.Property(x => x.Status).HasMaxLength(32);
            e.Property(x => x.RewardAmount).HasPrecision(12, 2);
            e.Property(x => x.CvUrl).HasMaxLength(1024);
            e.Property(x => x.Notes).HasColumnType("text");
            e.Property(x => x.PaymentStatus).HasMaxLength(32).HasDefaultValue(ReferralPaymentStatus.NotEligible);
            e.Property(x => x.PaidByUserId).HasMaxLength(128);
            e.Property(x => x.PaidByLabel).HasMaxLength(256);
            e.Property(x => x.PaymentReference).HasMaxLength(256);
            e.HasIndex(x => x.ReferrerId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.PaymentStatus);
            e.HasIndex(x => x.CandidateEmail);
        });

        modelBuilder.Entity<ReferralHistoryEntryEntity>(e =>
        {
            e.ToTable("parrainage_referral_history");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(160);
            e.Property(x => x.ReferralId).HasMaxLength(128);
            e.Property(x => x.CandidateName).HasMaxLength(256);
            e.Property(x => x.Action).HasMaxLength(32);
            e.Property(x => x.PerformedById).HasMaxLength(128);
            e.Property(x => x.PerformedByLabel).HasMaxLength(256);
            e.Property(x => x.Comment).HasMaxLength(2048);
            e.Property(x => x.RewardAmount).HasPrecision(12, 2);
            e.HasIndex(x => x.ReferralId);
            e.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<ReferralRuleEntity>(e =>
        {
            e.ToTable("parrainage_referral_rule");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(128);
            e.Property(x => x.Name).HasMaxLength(256);
            e.Property(x => x.Type).HasMaxLength(64);
            e.Property(x => x.Value).HasPrecision(12, 2);
            e.Property(x => x.Target).HasMaxLength(256);
            e.Property(x => x.MinDurationMonths).HasDefaultValue(6);
            e.Property(x => x.Status).HasMaxLength(32);
        });

        modelBuilder.Entity<ReferralNotificationEntity>(e =>
        {
            e.ToTable("parrainage_referral_notification");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(160);
            e.Property(x => x.Type).HasMaxLength(64);
            e.Property(x => x.Message).HasMaxLength(1024);
            e.Property(x => x.ReferralId).HasMaxLength(128);
            e.Property(x => x.ReferrerId).HasMaxLength(128);
            e.Property(x => x.TargetRoles)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);
            e.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<NotificationPreferenceEntity>(e =>
        {
            e.ToTable("parrainage_notification_preference");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<SystemConfigEntity>(e =>
        {
            e.ToTable("parrainage_system_config");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.ReferralProgramRules)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonOpts),
                    v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<ReferralProgramRules>(v, JsonOpts));
            e.Property(x => x.AdminWorkflow)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonOpts),
                    v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<AdminWorkflowConfig>(v, JsonOpts));
        });

        modelBuilder.Entity<AuditLogEntryEntity>(e =>
        {
            e.ToTable("parrainage_audit_log");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(160);
            e.Property(x => x.Action).HasMaxLength(128);
            e.Property(x => x.UserId).HasMaxLength(128);
            e.Property(x => x.UserLabel).HasMaxLength(256);
            e.Property(x => x.Details).HasMaxLength(8192);
            e.HasIndex(x => x.Timestamp);
        });
    }
}
