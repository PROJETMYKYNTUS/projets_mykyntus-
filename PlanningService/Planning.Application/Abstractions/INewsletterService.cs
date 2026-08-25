using Planning.Application.DTOs.Newsletter;

namespace Planning.Application.Abstractions
{
    public interface INewsletterService
    {
        // -- Newsletters (contenu) ----------------------------------------------
        Task<IEnumerable<NewsletterResponseDto>> GetAllNewslettersAsync();
        Task<NewsletterResponseDto?> GetNewsletterByIdAsync(int id);
        Task<NewsletterResponseDto> CreateNewsletterAsync(CreateNewsletterDto dto, string userId);
        Task<NewsletterResponseDto?> UpdateNewsletterAsync(int id, UpdateNewsletterDto dto);
        Task<bool> DeleteNewsletterAsync(int id);

        // -- Campaigns ---------------------------------------------------------
        Task<IEnumerable<CampaignResponseDto>> GetAllCampaignsAsync();
        Task<CampaignResponseDto?> GetCampaignByIdAsync(int id);
        Task<CampaignResponseDto> CreateCampaignAsync(CreateCampaignDto dto, string userId);
        Task<bool> PublishCampaignAsync(int campaignId);
        Task<bool> ScheduleCampaignAsync(int campaignId, DateTime scheduledAt);
        Task<bool> CancelCampaignAsync(int campaignId);

        // -- Côté Employee / Manager : newsletters reçues ----------------------
        Task<IEnumerable<EmployeeNewsletterDto>> GetNewslettersForEmployeeAsync(string userId, string? email = null);
        Task<bool> MarkAsReadAsync(int analyticsId, string userId, string? email = null);

        /// <summary>Corrige les UserId analytics (id planning ? AuthUserId JWT).</summary>
        Task RepairCampaignAnalyticsUserIdsAsync();

        // -- Analytics ---------------------------------------------------------
        Task<CampaignAnalyticsDto?> GetCampaignAnalyticsAsync(int campaignId);

        /// <summary>Formulaire unique : crée contenu + campagne (+ publie ou planifie).</summary>
        Task<PublicationResponseDto> CreatePublicationAsync(CreatePublicationDto dto, string userId);
    }
}