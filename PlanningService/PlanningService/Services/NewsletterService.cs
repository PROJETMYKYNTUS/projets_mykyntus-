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
        private readonly IUserService _userService;
        private readonly ILogger<NewsletterService> _logger;

        public NewsletterService(
            AppDbContext context,
            IHubContext<NewsletterHub> hubContext,
            IUserService userService,
            ILogger<NewsletterService> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _userService = userService;
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

            await _userService.SyncMissingAuthUsersAsync();

            var users = await GetUsersByAudienceAsync(campaign.AudienceTarget);
            if (users.Count == 0)
            {
                _logger.LogWarning(
                    "Campagne [{Id}] : aucun destinataire pour l'audience {Audience}",
                    campaignId, campaign.AudienceTarget);
                return false;
            }

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
                    var currentUserIdStr = ResolveStoredAnalyticsUserId(user);

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
        public async Task<IEnumerable<EmployeeNewsletterDto>> GetNewslettersForEmployeeAsync(string userId, string? email = null)
        {
            var userIds = await ResolveAnalyticsUserIdsAsync(userId, email);

            var results = await _context.CampaignAnalytics
                .AsNoTracking()
                .Include(a => a.Campaign).ThenInclude(c => c.Newsletter)
                .Where(a => userIds.Contains(a.UserId)
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

            _logger.LogInformation(
                "Newsletters inbox userId={UserId} email={Email} ids=[{Ids}] → {Count} résultat(s)",
                userId, email ?? "-", string.Join(',', userIds), results.Count);

            return results;
        }

        /// <summary>Marque une newsletter comme lue.</summary>
        public async Task<bool> MarkAsReadAsync(int analyticsId, string userId, string? email = null)
        {
            var userIds = await ResolveAnalyticsUserIdsAsync(userId, email);
            var analytics = await _context.CampaignAnalytics
                .FirstOrDefaultAsync(a => a.Id == analyticsId && userIds.Contains(a.UserId));

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
                AudienceTarget.Employees => "Employee",
                AudienceTarget.Managers => "Manager",
                AudienceTarget.Admins => "Admin",
                AudienceTarget.Pilotes => "Pilote",
                AudienceTarget.Coaches => "Coach",
                AudienceTarget.RPs => "RP",
                AudienceTarget.Audits => "Audit",
                AudienceTarget.EquipeFormation => "EquipeFormation",
                _ => audience.ToString()
            };
            _logger.LogInformation("Recherche users avec rôle: '{Role}'", targetRoleName);

            var users = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.IsActive && u.Role.Name == targetRoleName)
                .ToListAsync();

            _logger.LogInformation("Users trouvés: {Count}", users.Count);

            return users;
        }
        /// <summary>
        /// Remplace les UserId analytics (id planning) par AuthUserId quand le lien existe.
        /// </summary>
        public async Task RepairCampaignAnalyticsUserIdsAsync()
        {
            var usersByPlanningId = await _context.Users
                .Where(u => u.AuthUserId != null)
                .ToDictionaryAsync(u => u.Id.ToString(), u => u);

            var analytics = await _context.CampaignAnalytics.ToListAsync();
            var repaired = 0;

            foreach (var entry in analytics)
            {
                if (!usersByPlanningId.TryGetValue(entry.UserId, out var user))
                    continue;

                var canonical = user.AuthUserId!.Value.ToString();
                if (entry.UserId == canonical)
                    continue;

                var duplicate = analytics.Any(a =>
                    a.Id != entry.Id &&
                    a.CampaignId == entry.CampaignId &&
                    a.UserId == canonical);

                if (duplicate)
                    continue;

                entry.UserId = canonical;
                repaired++;
            }

            if (repaired > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("CampaignAnalytics : {Count} UserId corrigé(s) vers AuthUserId", repaired);
            }
        }

        private static string ResolveStoredAnalyticsUserId(User user) =>
            user.AuthUserId?.ToString() ?? user.Id.ToString();

        /// <summary>
        /// JWT NameIdentifier (AuthUserId) peut différer de l'id planning stocké en analytics.
        /// </summary>
        private async Task<List<string>> ResolveAnalyticsUserIdsAsync(string userId, string? email = null)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal) { userId };

            if (!string.IsNullOrWhiteSpace(email))
            {
                var byEmail = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Email == email);

                if (byEmail != null)
                {
                    ids.Add(byEmail.Id.ToString());
                    if (byEmail.AuthUserId.HasValue)
                        ids.Add(byEmail.AuthUserId.Value.ToString());
                }
            }

            if (int.TryParse(userId, out var numericId))
            {
                var planningUser = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.AuthUserId == numericId || u.Id == numericId);

                if (planningUser != null)
                {
                    ids.Add(planningUser.Id.ToString());
                    if (planningUser.AuthUserId.HasValue)
                        ids.Add(planningUser.AuthUserId.Value.ToString());
                }
            }

            return ids.ToList();
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