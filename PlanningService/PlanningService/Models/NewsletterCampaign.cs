using PlanningService.Enums;

namespace PlanningService.Models
{
    public class NewsletterCampaign
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int NewsletterId { get; set; }
        public AudienceTarget AudienceTarget { get; set; } = AudienceTarget.All;
        public CampaignStatus Status { get; set; } = CampaignStatus.Draft;
        public DateTime? ScheduledAt { get; set; }
        public DateTime? PublishedAt { get; set; }   // Remplace SentAt (pas d'email)
        public int TotalRecipients { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string CreatedByUserId { get; set; } = string.Empty;

        // Navigation
        public Newsletter Newsletter { get; set; } = null!;
        public ICollection<CampaignAnalytics> Analytics { get; set; } = new List<CampaignAnalytics>();
    }
}