using PlanningService.Enums;

namespace PlanningService.DTOs.Newsletter
{
    // ─── Newsletter (Contenu) ───────────────────────────────────────────────────

    public class CreateNewsletterDto
    {
        public string Title { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string HtmlContent { get; set; } = string.Empty;
        public string? TextContent { get; set; }
    }

    public class UpdateNewsletterDto
    {
        public string? Title { get; set; }
        public string? Subject { get; set; }
        public string? HtmlContent { get; set; }
        public string? TextContent { get; set; }
    }

    public class NewsletterResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string HtmlContent { get; set; } = string.Empty;
        public string? TextContent { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
        public int CampaignsCount { get; set; }
    }

    // ─── Campaign ──────────────────────────────────────────────────────────────

    public class CreateCampaignDto
    {
        public string Name { get; set; } = string.Empty;
        public int NewsletterId { get; set; }
        public AudienceTarget AudienceTarget { get; set; } = AudienceTarget.All;
        public DateTime? ScheduledAt { get; set; }
    }

    public class CampaignResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int NewsletterId { get; set; }
        public string NewsletterTitle { get; set; } = string.Empty;
        public string NewsletterSubject { get; set; } = string.Empty;
        public AudienceTarget AudienceTarget { get; set; }
        public CampaignStatus Status { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public DateTime? PublishedAt { get; set; }
        public int TotalRecipients { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ─── Vue Employee : newsletter reçue dans le dashboard ─────────────────────

    public class EmployeeNewsletterDto
    {
        public int AnalyticsId { get; set; }           // Pour marquer comme lu
        public int CampaignId { get; set; }
        public string CampaignName { get; set; } = string.Empty;
        public string NewsletterTitle { get; set; } = string.Empty;
        public string NewsletterSubject { get; set; } = string.Empty;
        public string HtmlContent { get; set; } = string.Empty;
        public string? TextContent { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime ReceivedAt { get; set; }
    }

    // ─── Notification SignalR (payload poussé en temps réel) ───────────────────

    public class NewsletterNotificationDto
    {
        public int CampaignId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }

    // ─── Subscriber ────────────────────────────────────────────────────────────

    public class SubscribeDto
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public AudienceTarget Group { get; set; } = AudienceTarget.All;
        public string? UserId { get; set; }
    }

    public class SubscriberResponseDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public AudienceTarget Group { get; set; }
        public bool IsActive { get; set; }
        public DateTime SubscribedAt { get; set; }
    }

    // ─── Analytics ─────────────────────────────────────────────────────────────

    public class CampaignAnalyticsDto
    {
        public int CampaignId { get; set; }
        public string CampaignName { get; set; } = string.Empty;
        public int TotalRecipients { get; set; }
        public int TotalRead { get; set; }
        public int TotalUnread { get; set; }
        public double ReadRate { get; set; }
    }
}