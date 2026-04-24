namespace PlanningService.Models
{
    public class Newsletter
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string HtmlContent { get; set; } = string.Empty;
        public string? TextContent { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;

        // Navigation
        public ICollection<NewsletterCampaign> Campaigns { get; set; } = new List<NewsletterCampaign>();
    }
}