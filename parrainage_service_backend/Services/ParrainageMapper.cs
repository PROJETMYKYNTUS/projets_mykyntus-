using ParrainageBackend.Data;
using ParrainageBackend.Dto;

namespace ParrainageBackend.Services;

/// <summary>Entity → DTO projections so JSON shapes match the Angular interfaces.</summary>
public static class ParrainageMapper
{
    public static ReferralDto ToDto(this ReferralEntity e) => new()
    {
        Id = e.Id,
        ReferrerId = e.ReferrerId,
        ReferrerName = e.ReferrerName,
        ProjectId = e.ProjectId,
        ProjectName = e.ProjectName,
        TeamId = e.TeamId,
        CandidateName = e.CandidateName,
        CandidateEmail = e.CandidateEmail,
        CandidatePhone = e.CandidatePhone,
        Position = e.Position,
        Status = e.Status,
        RewardAmount = e.RewardAmount,
        CvUrl = e.CvUrl,
        Notes = e.Notes,
        CandidateStartDate = e.CandidateStartDate,
        ApprovedAt = e.ApprovedAt,
        EligibleForPaymentAt = e.EligibleForPaymentAt,
        PaymentStatus = e.PaymentStatus,
        PaidAt = e.PaidAt,
        PaidByUserId = e.PaidByUserId,
        PaidByLabel = e.PaidByLabel,
        PaymentReference = e.PaymentReference,
        CreatedAt = e.CreatedAt,
    };

    public static ReferralHistoryDto ToDto(this ReferralHistoryEntryEntity e) => new()
    {
        Id = e.Id,
        ReferralId = e.ReferralId,
        CandidateName = e.CandidateName,
        Action = e.Action,
        PerformedById = e.PerformedById,
        PerformedByLabel = e.PerformedByLabel,
        CreatedAt = e.CreatedAt,
        Comment = e.Comment,
        RewardAmount = e.RewardAmount,
    };

    public static ReferralRuleDto ToDto(this ReferralRuleEntity e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Type = e.Type,
        Value = e.Value,
        Target = e.Target,
        Status = e.Status,
        CreatedAt = e.CreatedAt,
    };

    public static ReferralNotificationDto ToDto(this ReferralNotificationEntity e) => new()
    {
        Id = e.Id,
        Type = e.Type,
        Message = e.Message,
        CreatedAt = e.CreatedAt,
        Read = e.Read,
        ReferralId = e.ReferralId,
        ReferrerId = e.ReferrerId,
        TargetRoles = e.TargetRoles.Count > 0 ? e.TargetRoles : null,
    };

    public static NotificationPreferencesDto ToDto(this NotificationPreferenceEntity e) => new()
    {
        Email = e.Email,
        InApp = e.InApp,
        SystemAlerts = e.SystemAlerts,
        Referrals = e.Referrals,
        Approvals = e.Approvals,
        Payments = e.Payments,
    };

    public static SystemConfigDto ToDto(this SystemConfigEntity e) => new()
    {
        DefaultBonusAmount = e.DefaultBonusAmount,
        MinDurationMonths = e.MinDurationMonths,
        ReferralLimitPerEmployee = e.ReferralLimitPerEmployee,
        PendingReferralAlertThreshold = e.PendingReferralAlertThreshold,
        ReferralProgramRules = e.ReferralProgramRules,
        AdminWorkflow = e.AdminWorkflow,
    };

    public static AuditLogDto ToDto(this AuditLogEntryEntity e) => new()
    {
        Id = e.Id,
        Action = e.Action,
        UserId = e.UserId,
        UserLabel = e.UserLabel,
        Timestamp = e.Timestamp,
        Details = e.Details,
    };
}
