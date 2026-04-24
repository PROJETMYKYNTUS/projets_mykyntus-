using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PlanningService.Data;
using PlanningService.DTOs.Newsletter;
using PlanningService.Enums;
using PlanningService.Hubs;
using PlanningService.Interfaces;
using PlanningService.Models;

namespace PlanningService.Services
{
    public class NewsletterService : INewsletterService
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NewsletterHub> _hubContext;
        private readonly ILogger<NewsletterService> _logger;

        public NewsletterService(
            AppDbContext context,
            IHubContext<NewsletterHub> hubContext,
            ILogger<NewsletterService> logger) // Plus de UserManager ici
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        // ────────────────────────────────────────────────────────────────────────
        // NEWSLETTERS (contenu)
        // ────────────────────────────────────────────────────────────────────────

        public async Task<IEnumerable<NewsletterResponseDto>> GetAllNewslettersAsync()
        {
            return await _context.Newsletters
                .AsNoTracking()
                .Select(n => new NewsletterResponseDto
                {
                    Id = n.Id,
                    Title = n.Title,
                    Subject = n.Subject,
                    HtmlContent = n.HtmlContent,
                    TextContent = n.TextContent,
                    CreatedAt = n.CreatedAt,
                    UpdatedAt = n.UpdatedAt,
                    CreatedByUserId = n.CreatedByUserId,
                    CampaignsCount = _context.NewsletterCampaigns.Count(c => c.NewsletterId == n.Id)
                })
                .ToListAsync();
        }


        public async Task<NewsletterResponseDto?> GetNewsletterByIdAsync(int id)
        {
            var n = await _context.Newsletters
                .AsNoTracking()
                .Include(x => x.Campaigns)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (n is null) return null;

            return new NewsletterResponseDto
            {
                Id = n.Id,
                Title = n.Title,
                Subject = n.Subject,
                HtmlContent = n.HtmlContent,
                TextContent = n.TextContent,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt,
                CreatedByUserId = n.CreatedByUserId,
                CampaignsCount = n.Campaigns.Count
            };
        }

        public async Task<NewsletterResponseDto> CreateNewsletterAsync(CreateNewsletterDto dto, string userId)
        {
            var newsletter = new Newsletter
            {
                Title = dto.Title,
                Subject = dto.Subject,
                HtmlContent = dto.HtmlContent,
                TextContent = dto.TextContent,
                CreatedByUserId = userId
            };

            _context.Newsletters.Add(newsletter);
            await _context.SaveChangesAsync();
            return (await GetNewsletterByIdAsync(newsletter.Id))!;
        }

        public async Task<NewsletterResponseDto?> UpdateNewsletterAsync(int id, UpdateNewsletterDto dto)
        {
            var newsletter = await _context.Newsletters.FindAsync(id);
            if (newsletter is null) return null;

            if (dto.Title is not null)      newsletter.Title = dto.Title;
            if (dto.Subject is not null)    newsletter.Subject = dto.Subject;
            if (dto.HtmlContent is not null) newsletter.HtmlContent = dto.HtmlContent;
            if (dto.TextContent is not null) newsletter.TextContent = dto.TextContent;
            newsletter.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return await GetNewsletterByIdAsync(id);
        }

        public async Task<bool> DeleteNewsletterAsync(int id)
        {
            var newsletter = await _context.Newsletters.FindAsync(id);
            if (newsletter is null) return false;

            _context.Newsletters.Remove(newsletter);
            await _context.SaveChangesAsync();
            return true;
        }

        // ────────────────────────────────────────────────────────────────────────
        // CAMPAIGNS
        // ────────────────────────────────────────────────────────────────────────

        public async Task<IEnumerable<CampaignResponseDto>> GetAllCampaignsAsync()
        {
            return await _context.NewsletterCampaigns
                .AsNoTracking()
                .Select(c => new CampaignResponseDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    NewsletterId = c.NewsletterId,
                    NewsletterTitle = c.Newsletter != null ? c.Newsletter.Title : string.Empty,
                    NewsletterSubject = c.Newsletter != null ? c.Newsletter.Subject : string.Empty,
                    AudienceTarget = c.AudienceTarget,
                    Status = c.Status,
                    ScheduledAt = c.ScheduledAt,
                    PublishedAt = c.PublishedAt,
                    TotalRecipients = c.TotalRecipients,
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync();
        }


        public async Task<CampaignResponseDto?> GetCampaignByIdAsync(int id)
        {
            var campaign = await _context.NewsletterCampaigns
                .AsNoTracking()
                .Include(c => c.Newsletter)
                .FirstOrDefaultAsync(c => c.Id == id);

            return campaign is null ? null : MapCampaignToDto(campaign);
        }

        public async Task<CampaignResponseDto> CreateCampaignAsync(CreateCampaignDto dto, string userId)
        {
            var campaign = new NewsletterCampaign
            {
                Name = dto.Name,
                NewsletterId = dto.NewsletterId,
                AudienceTarget = dto.AudienceTarget,
                ScheduledAt = dto.ScheduledAt,
                Status = dto.ScheduledAt.HasValue ? CampaignStatus.Scheduled : CampaignStatus.Draft,
                CreatedByUserId = userId
            };

            _context.NewsletterCampaigns.Add(campaign);
            await _context.SaveChangesAsync();
            return (await GetCampaignByIdAsync(campaign.Id))!;
        }

        /// <summary>
        /// Publie la newsletter dans les dashboards des employés/managers ciblés
        /// selon leur rôle Identity, et envoie une notification SignalR en temps réel.
        /// </summary>
        public async Task<bool> PublishCampaignAsync(int campaignId)
        {
            var campaign = await _context.NewsletterCampaigns
                .Include(c => c.Newsletter)
                .FirstOrDefaultAsync(c => c.Id == campaignId);

            if (campaign is null || campaign.Status == CampaignStatus.Sent)
                return false;

            // Récupérer les users selon le rôle (remplace la table Subscribers)
            var users = await GetUsersByAudienceAsync(campaign.AudienceTarget);

            campaign.Status = CampaignStatus.Sending;
            campaign.TotalRecipients = users.Count;
            await _context.SaveChangesAsync();

            // ── Transaction pour éviter des données partielles ────────────────
            // ── Transaction pour éviter des données partielles ────────────────
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // Créer un CampaignAnalytics par user ciblé
                foreach (var user in users)
                {
                    // Conversion explicite pour éviter l'erreur de type
                    string currentUserIdStr = user.Id.ToString();

                    var alreadyExists = await _context.CampaignAnalytics
                        .AnyAsync(a => a.UserId == currentUserIdStr && a.CampaignId == campaign.Id);

                    if (!alreadyExists)
                    {
                        _context.CampaignAnalytics.Add(new CampaignAnalytics
                        {
                            CampaignId = campaign.Id,
                            UserId = currentUserIdStr, // On stocke le string
                            ReceivedAt = DateTime.UtcNow,
                            IsRead = false
                        });
                    }
                }
                campaign.Status = CampaignStatus.Sent;
                campaign.PublishedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Erreur lors de la publication de la campagne [{Id}]", campaignId);
                campaign.Status = CampaignStatus.Draft;
                await _context.SaveChangesAsync();
                return false;
            }

            // ── Notification SignalR ──────────────────────────────────────────
            var notification = new NewsletterNotificationDto
            {
                CampaignId = campaign.Id,
                Title = campaign.Newsletter.Title,
                Subject = campaign.Newsletter.Subject,
                SentAt = campaign.PublishedAt!.Value
            };

            var signalRGroup = campaign.AudienceTarget switch
            {
                AudienceTarget.Employees => "Employee",
                AudienceTarget.Managers  => "Manager",
                AudienceTarget.Admins    => "Admin",
                _                        => "All"
            };

            await _hubContext.Clients
                .Group(signalRGroup)
                .SendAsync("ReceiveNewsletter", notification);

            _logger.LogInformation(
                "Campagne [{Id}] publiée vers le groupe '{Group}' ({Count} destinataires)",
                campaignId, signalRGroup, users.Count);

            return true;
        }

        public async Task<bool> ScheduleCampaignAsync(int campaignId, DateTime scheduledAt)
        {
            var campaign = await _context.NewsletterCampaigns.FindAsync(campaignId);
            if (campaign is null) return false;

            campaign.ScheduledAt = scheduledAt;
            campaign.Status = CampaignStatus.Scheduled;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> CancelCampaignAsync(int campaignId)
        {
            var campaign = await _context.NewsletterCampaigns.FindAsync(campaignId);
            if (campaign is null || campaign.Status == CampaignStatus.Sent) return false;

            campaign.Status = CampaignStatus.Cancelled;
            await _context.SaveChangesAsync();
            return true;
        }

        // ────────────────────────────────────────────────────────────────────────
        // CÔTÉ EMPLOYEE / MANAGER
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Retourne toutes les newsletters reçues dans le dashboard de l'utilisateur connecté.
        /// Filtre directement par UserId dans CampaignAnalytics (plus de SubscriberId).
        /// </summary>
        public async Task<IEnumerable<EmployeeNewsletterDto>> GetNewslettersForEmployeeAsync(string userId)
        {
            return await _context.CampaignAnalytics
                .AsNoTracking()
                .Include(a => a.Campaign).ThenInclude(c => c.Newsletter)
                .Where(a => a.UserId == userId
                         && a.Campaign.Status == CampaignStatus.Sent)
                .OrderByDescending(a => a.ReceivedAt)
                .Select(a => new EmployeeNewsletterDto
                {
                    AnalyticsId      = a.Id,
                    CampaignId       = a.CampaignId,
                    CampaignName     = a.Campaign.Name,
                    NewsletterTitle  = a.Campaign.Newsletter.Title,
                    NewsletterSubject = a.Campaign.Newsletter.Subject,
                    HtmlContent      = a.Campaign.Newsletter.HtmlContent,
                    TextContent      = a.Campaign.Newsletter.TextContent,
                    IsRead           = a.IsRead,
                    ReadAt           = a.ReadAt,
                    ReceivedAt       = a.ReceivedAt
                })
                .ToListAsync();
        }

        /// <summary>Marque une newsletter comme lue.</summary>
        // Vérifiez que 'userId' est bien présent dans les paramètres (string)
        public async Task<bool> MarkAsReadAsync(int analyticsId, string userId)
        {
            // On compare string (a.UserId) avec string (userId)
            var analytics = await _context.CampaignAnalytics
                .FirstOrDefaultAsync(a => a.Id == analyticsId && a.UserId == userId);

            if (analytics is null || analytics.IsRead) return false;

            analytics.IsRead = true;
            analytics.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        // ────────────────────────────────────────────────────────────────────────
        // ANALYTICS
        // ────────────────────────────────────────────────────────────────────────

        public async Task<CampaignAnalyticsDto?> GetCampaignAnalyticsAsync(int campaignId)
        {
            var campaign = await _context.NewsletterCampaigns
                .AsNoTracking()
                .Include(c => c.Analytics)
                .FirstOrDefaultAsync(c => c.Id == campaignId);

            if (campaign is null) return null;

            var total = campaign.Analytics.Count;
            var read  = campaign.Analytics.Count(a => a.IsRead);

            return new CampaignAnalyticsDto
            {
                CampaignId      = campaign.Id,
                CampaignName    = campaign.Name,
                TotalRecipients = campaign.TotalRecipients,
                TotalRead       = read,
                TotalUnread     = total - read,
                ReadRate        = total > 0 ? Math.Round((double)read / total * 100, 2) : 0
            };
        }

        // ────────────────────────────────────────────────────────────────────────
        // HELPERS
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Récupère les users par rôle Identity selon l'audience de la campagne.
        /// Remplace complètement la table NewsletterSubscribers.
        /// </summary>
        private async Task<List<User>> GetUsersByAudienceAsync(AudienceTarget audience)
        {
            if (audience == AudienceTarget.All)
                return await _context.Users.ToListAsync();

            // Map explicite : enum pluriel → nom de rôle singulier en BDD
            string targetRoleName = audience switch
            {
                AudienceTarget.Employees => "EMPLOYEE",
                AudienceTarget.Managers => "MANAGER",
                AudienceTarget.Admins => "Admin",
                AudienceTarget.Pilotes => "Pilote",
                AudienceTarget.Coaches => "Coach",
                AudienceTarget.RPs => "RP",
                AudienceTarget.Audits => "Audit",
                AudienceTarget.EquipeFormation => "Equipe formation",
                _ => audience.ToString()
            };
            _logger.LogInformation("Recherche users avec rôle: '{Role}'", targetRoleName);

            var users = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role.Name == targetRoleName)
                .ToListAsync();

            _logger.LogInformation("Users trouvés: {Count}", users.Count);

            return users;
        }
        private static CampaignResponseDto MapCampaignToDto(NewsletterCampaign c) => new()
        {
            Id               = c.Id,
            Name             = c.Name,
            NewsletterId     = c.NewsletterId,
            NewsletterTitle  = c.Newsletter?.Title ?? string.Empty,
            NewsletterSubject = c.Newsletter?.Subject ?? string.Empty,
            AudienceTarget   = c.AudienceTarget,
            Status           = c.Status,
            ScheduledAt      = c.ScheduledAt,
            PublishedAt      = c.PublishedAt,
            TotalRecipients  = c.TotalRecipients,
            CreatedAt        = c.CreatedAt
        };
    }
}