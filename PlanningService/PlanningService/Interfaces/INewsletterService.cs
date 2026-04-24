using PlanningService.DTOs.Newsletter;

namespace PlanningService.Interfaces
{
    public interface INewsletterService
    {
        // ── Newsletters (contenu) ──────────────────────────────────────────────
        Task<IEnumerable<NewsletterResponseDto>> GetAllNewslettersAsync();
        Task<NewsletterResponseDto?> GetNewsletterByIdAsync(int id);
        Task<NewsletterResponseDto> CreateNewsletterAsync(CreateNewsletterDto dto, string userId);
        Task<NewsletterResponseDto?> UpdateNewsletterAsync(int id, UpdateNewsletterDto dto);
        Task<bool> DeleteNewsletterAsync(int id);

        // ── Campaigns ─────────────────────────────────────────────────────────
        Task<IEnumerable<CampaignResponseDto>> GetAllCampaignsAsync();
        Task<CampaignResponseDto?> GetCampaignByIdAsync(int id);
        Task<CampaignResponseDto> CreateCampaignAsync(CreateCampaignDto dto, string userId);
        Task<bool> PublishCampaignAsync(int campaignId);
        Task<bool> ScheduleCampaignAsync(int campaignId, DateTime scheduledAt);
        Task<bool> CancelCampaignAsync(int campaignId);

        // ── Côté Employee / Manager : newsletters reçues ──────────────────────
        Task<IEnumerable<EmployeeNewsletterDto>> GetNewslettersForEmployeeAsync(string userId);
        Task<bool> MarkAsReadAsync(int analyticsId, string userId);

        // ── Analytics ─────────────────────────────────────────────────────────
        Task<CampaignAnalyticsDto?> GetCampaignAnalyticsAsync(int campaignId);
    }
}