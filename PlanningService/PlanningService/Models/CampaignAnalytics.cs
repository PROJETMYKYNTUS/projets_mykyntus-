using PlanningService.Models;

public class CampaignAnalytics
{
    public int Id { get; set; }
    public int CampaignId { get; set; }

    // CHANGEZ CECI : de 'int' à 'string'
    public string UserId { get; set; } = string.Empty;

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime ReceivedAt { get; set; }

    // Supprimez toute référence à 'Subscriber' ou 'SubscriberId' ici
    public virtual NewsletterCampaign Campaign { get; set; } = null!;
}