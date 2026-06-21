using ParrainageBackend.Models;

namespace ParrainageBackend.Data;

public class ReferralEntity
{
    public string Id { get; set; } = string.Empty;
    public string ReferrerId { get; set; } = string.Empty;
    public string ReferrerName { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateEmail { get; set; } = string.Empty;
    public string CandidatePhone { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string PositionMode { get; set; } = ReferralPositionMode.Custom;
    public string? AppliedRuleId { get; set; }
    public string Status { get; set; } = "SUBMITTED";
    public decimal RewardAmount { get; set; }
    public string? CvUrl { get; set; }
    public string? Notes { get; set; }
    public DateOnly? CandidateStartDate { get; set; }
    public DateOnly? TrainingEndDate { get; set; }
    public DateOnly? ProductionStartDate { get; set; }
    public DateTimeOffset? TrainingEndNotifiedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? EligibleForPaymentAt { get; set; }
    public string PaymentStatus { get; set; } = ReferralPaymentStatus.NotEligible;
    public DateTimeOffset? PaidAt { get; set; }
    public string? PaidByUserId { get; set; }
    public string? PaidByLabel { get; set; }
    public string? PaymentReference { get; set; }
    public DateTimeOffset? EligibilityNotifiedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class ReferralHistoryEntryEntity
{
    public string Id { get; set; } = string.Empty;
    public string ReferralId { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string PerformedById { get; set; } = string.Empty;
    public string PerformedByLabel { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string? Comment { get; set; }
    public decimal? RewardAmount { get; set; }
}

public class ReferralRuleEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string? Target { get; set; }
    public int MinDurationMonths { get; set; } = 6;
    public string Status { get; set; } = "ACTIVE";
    public DateTimeOffset CreatedAt { get; set; }
}

public class ReferralNotificationEntity
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public bool Read { get; set; }
    public string? ReferralId { get; set; }
    public string? ReferrerId { get; set; }
    public List<string> TargetRoles { get; set; } = new();
}

// Single-row table holding the global in-app notification preferences.
public class NotificationPreferenceEntity
{
    public int Id { get; set; } = 1;
    public bool Email { get; set; } = true;
    public bool InApp { get; set; } = true;
    public bool SystemAlerts { get; set; } = true;
    public bool Referrals { get; set; } = true;
    public bool Approvals { get; set; } = true;
    public bool Payments { get; set; } = true;
}

// Single-row table holding the system configuration. Nested objects are stored as JSON.
public class SystemConfigEntity
{
    public int Id { get; set; } = 1;
    public int DefaultBonusAmount { get; set; }
    public int MinDurationMonths { get; set; }
    public int ReferralLimitPerEmployee { get; set; }
    public int? PendingReferralAlertThreshold { get; set; }
    public ReferralProgramRules? ReferralProgramRules { get; set; }
    public AdminWorkflowConfig? AdminWorkflow { get; set; }
}

public class AuditLogEntryEntity
{
    public string Id { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserLabel { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string? Details { get; set; }
}
