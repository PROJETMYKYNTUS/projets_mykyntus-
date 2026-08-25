using Planning.Domain.Enums;
using Planning.Application.DTOs;

namespace Planning.Application.DTOs.Newsletter
{
    // --- Newsletter (Contenu) ---------------------------------------------------

    public class CreateNewsletterDto
    {
        public string Title { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string TextContent { get; set; } = string.Empty;
        public string? CoverImageUrl { get; set; }
        public List<int>? MediaIds { get; set; }
    }

    public class UpdateNewsletterDto
    {
        public string? Title { get; set; }
        public string? Subject { get; set; }
        public string? TextContent { get; set; }
        public string? CoverImageUrl { get; set; }
        public List<int>? MediaIds { get; set; }
    }

    public class NewsletterResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string HtmlContent { get; set; } = string.Empty;
        public string? TextContent { get; set; }
        public string? CoverImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
        public int CampaignsCount { get; set; }
        public List<MediaAssetDto> Media { get; set; } = new();
    }

    /// <summary>Formulaire unique : contenu + audience + publier/planifier en une requÍte.</summary>
    public class CreatePublicationDto
    {
        public string Title { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string TextContent { get; set; } = string.Empty;
        public List<int>? MediaIds { get; set; }
        public AudienceTarget AudienceTarget { get; set; } = AudienceTarget.Custom;
        /// <summary>GUIDs employÈs sÈlectionnÈs (mÍme picker que formation continue).</summary>
        public List<string>? BeneficiaryUserIds { get; set; }
        /// <summary>draft | publish | schedule</summary>
        public string Mode { get; set; } = "publish";
        public DateTime? ScheduledAt { get; set; }
        public string? CampaignName { get; set; }
    }

    public class PublicationResponseDto
    {
        public NewsletterResponseDto Newsletter { get; set; } = null!;
        public CampaignResponseDto? Campaign { get; set; }
    }

    // --- Campaign --------------------------------------------------------------

    public class CreateCampaignDto
    {
        public string Name { get; set; } = string.Empty;
        public int NewsletterId { get; set; }
        public AudienceTarget AudienceTarget { get; set; } = AudienceTarget.All;
        public DateTime? ScheduledAt { get; set; }
        public List<string>? BeneficiaryUserIds { get; set; }
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

    // --- Vue Employee : newsletter reùue dans le dashboard ---------------------

    public class EmployeeNewsletterDto
    {
        public int AnalyticsId { get; set; }
        public int CampaignId { get; set; }
        public string CampaignName { get; set; } = string.Empty;
        public string NewsletterTitle { get; set; } = string.Empty;
        public string NewsletterSubject { get; set; } = string.Empty;
        public string HtmlContent { get; set; } = string.Empty;
        public string? TextContent { get; set; }
        public string? CoverImageUrl { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime ReceivedAt { get; set; }
        public List<MediaAssetDto> Media { get; set; } = new();
    }

    // --- Notification SignalR (payload poussù en temps rùel) -------------------

    public class NewsletterNotificationDto
    {
        public int CampaignId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }

    // --- Subscriber ------------------------------------------------------------

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

    // --- Analytics -------------------------------------------------------------

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
