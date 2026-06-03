using ParrainageBackend.Models;

namespace ParrainageBackend.Dto;

// ─── Response DTOs (camelCase JSON matches the Angular interfaces) ───

public sealed class ReferralDto
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
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? EligibleForPaymentAt { get; set; }
    public string PaymentStatus { get; set; } = "NOT_ELIGIBLE";
    public DateTimeOffset? PaidAt { get; set; }
    public string? PaidByUserId { get; set; }
    public string? PaidByLabel { get; set; }
    public string? PaymentReference { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ReferralHistoryDto
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

public sealed class ReferralRuleDto
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

public sealed class ReferralRuleCatalogDto
{
    public string RuleId { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public int MinDurationMonths { get; set; }
}

public sealed class ReferralRewardPreviewDto
{
    public decimal SuggestedAmount { get; set; }
    public int MinDurationMonths { get; set; }
    public string RuleLabel { get; set; } = string.Empty;
    public string? AppliedRuleId { get; set; }
    public string PositionMode { get; set; } = ReferralPositionMode.Custom;
}

public sealed class ReferralNotificationDto
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public bool Read { get; set; }
    public string? ReferralId { get; set; }
    public string? ReferrerId { get; set; }
    public List<string>? TargetRoles { get; set; }
}

public sealed class NotificationPreferencesDto
{
    public bool Email { get; set; }
    public bool InApp { get; set; }
    public bool? SystemAlerts { get; set; }
    public bool? Referrals { get; set; }
    public bool? Approvals { get; set; }
    public bool? Payments { get; set; }
}

public sealed class SystemConfigDto
{
    public int DefaultBonusAmount { get; set; }
    public int MinDurationMonths { get; set; }
    public int ReferralLimitPerEmployee { get; set; }
    public int? PendingReferralAlertThreshold { get; set; }
    public ReferralProgramRules? ReferralProgramRules { get; set; }
    public AdminWorkflowConfig? AdminWorkflow { get; set; }
}

public sealed class AuditLogDto
{
    public string Id { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserLabel { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string? Details { get; set; }
}

public sealed class AnomaliesDto
{
    public List<DuplicateCandidateDto> DuplicateCandidates { get; set; } = new();
    public List<SuspiciousEmailDto> SuspiciousEmails { get; set; } = new();
}

public sealed class DuplicateCandidateDto
{
    public string Email { get; set; } = string.Empty;
    public List<ReferralDto> Referrals { get; set; } = new();
}

public sealed class SuspiciousEmailDto
{
    public string Email { get; set; } = string.Empty;
    public int Count { get; set; }
    public List<string> ReferralIds { get; set; } = new();
}

// ─── Request DTOs ───

public sealed class ActorDto
{
    public string? Id { get; set; }
    public string? Label { get; set; }
    public string? Role { get; set; }
}

public sealed class CreateReferralRequest
{
    public string ReferrerId { get; set; } = string.Empty;
    public string ReferrerName { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateEmail { get; set; } = string.Empty;
    public string CandidatePhone { get; set; } = string.Empty;
    public string? RuleId { get; set; }
    public string Position { get; set; } = string.Empty;
    public string? Project { get; set; }
    public string? Notes { get; set; }
}

public sealed class UpdateReferralRequest
{
    public string? ReferrerName { get; set; }
    public string? ProjectName { get; set; }
    public string? CandidateName { get; set; }
    public string? CandidateEmail { get; set; }
    public string? CandidatePhone { get; set; }
    public string? Position { get; set; }
    public string? CvUrl { get; set; }
    public string? Status { get; set; }
    public decimal? RewardAmount { get; set; }
    public ActorDto? Actor { get; set; }
}

public sealed class ExportSnapshotDto
{
    public string ExportedAt { get; set; } = string.Empty;
    public List<ReferralDto> Referrals { get; set; } = new();
    public List<ReferralRuleDto> Rules { get; set; } = new();
    public List<ReferralHistoryDto> History { get; set; } = new();
    public List<ReferralNotificationDto> Notifications { get; set; } = new();
    public NotificationPreferencesDto? NotificationPreferences { get; set; }
    public SystemConfigDto? SystemConfig { get; set; }
    public List<AuditLogDto> AuditLog { get; set; } = new();
}

public sealed class UpdateStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public ActorDto? Actor { get; set; }
}

public sealed class RewardRequest
{
    public decimal Amount { get; set; }
    public ActorDto? Actor { get; set; }
}

public sealed class ApproveReferralRequest
{
    public DateOnly CandidateStartDate { get; set; }
    public decimal RewardAmount { get; set; }
    public string? Comment { get; set; }
    public ActorDto? Actor { get; set; }
}

public sealed class ProcessReferralRequest
{
    public string? Comment { get; set; }
    public ActorDto? Actor { get; set; }
}

public sealed class ConfirmPaymentEligibilityRequest
{
    public string? Comment { get; set; }
    public ActorDto? Actor { get; set; }
}

public sealed class MarkReferralPaymentRequest
{
    public bool Paid { get; set; } = true;
    public DateTimeOffset? PaidAt { get; set; }
    public string? Reference { get; set; }
    public ActorDto? Actor { get; set; }
}

public sealed class PaymentInboxDto
{
    public int ReadyCount { get; set; }
    public int PaidCount { get; set; }
    public int TotalApprovedCount { get; set; }
    public List<PaymentInboxItemDto> Items { get; set; } = new();
}

public sealed class PaymentInboxItemDto
{
    public ReferralDto Referral { get; set; } = new();
    public decimal Amount { get; set; }
    public bool CanMarkPaid { get; set; }
    public bool CanUndoPayment { get; set; }
}

public sealed class UpsertRuleRequest
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public decimal? Value { get; set; }
    public string? Target { get; set; }
    public int? MinDurationMonths { get; set; }
    public string? Status { get; set; }
}

public sealed class MarkReadRequest
{
    public string Id { get; set; } = string.Empty;
}

public sealed class UpdateConfigRequest
{
    public int? DefaultBonusAmount { get; set; }
    public int? MinDurationMonths { get; set; }
    public int? ReferralLimitPerEmployee { get; set; }
    public int? PendingReferralAlertThreshold { get; set; }
    public ReferralProgramRules? ReferralProgramRules { get; set; }
    public AdminWorkflowConfig? AdminWorkflow { get; set; }
    public ActorDto? Actor { get; set; }
}

public sealed class CreateAuditRequest
{
    public string Action { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? UserLabel { get; set; }
    public string? Details { get; set; }
}
