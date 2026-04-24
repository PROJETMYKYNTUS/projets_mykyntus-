// src/Services/ReclamationNotificationService.cs
using Microsoft.AspNetCore.SignalR;
using PlanningService.Hubs;
using PlanningService.Interfaces;

namespace PlanningService.Services
{
    public class ReclamationNotificationService : IReclamationNotificationService
    {
        private readonly IHubContext<ReclamationHub> _hub;
        private readonly ILogger<ReclamationNotificationService> _logger;

        public ReclamationNotificationService(
            IHubContext<ReclamationHub> hub,
            ILogger<ReclamationNotificationService> logger)
        {
            _hub = hub;
            _logger = logger;
        }

        // Notifier l'auteur de la réclamation/proposition
        public async Task NotifyAuteurAsync(string auteurId, string titre, string message, string type)
        {

            var groupName = $"user_{auteurId}";
            _logger.LogInformation("🔔 Envoi vers groupe: {Group}", groupName);
            await _hub.Clients
                .Group($"user_{auteurId}")
                .SendAsync("ReclamationNotification", new
                {
                    titre,
                    message,
                    type,       // "info" | "success" | "warning"
                    createdAt = DateTime.UtcNow
                });

            _logger.LogInformation("Notification envoyée à {UserId}: {Message}", auteurId, message);
        }

        // Notifier les gestionnaires (RH, Manager, RP, Admin)
        public async Task NotifyManagersAsync(string titre, string message, string type)
        {
            await _hub.Clients
                .Group("managers")
                .SendAsync("ReclamationNotification", new
                {
                    titre,
                    message,
                    type,
                    createdAt = DateTime.UtcNow
                });
        }
    }
}