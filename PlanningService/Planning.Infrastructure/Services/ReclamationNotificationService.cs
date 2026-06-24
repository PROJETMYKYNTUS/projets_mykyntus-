// src/Services/ReclamationNotificationService.cs
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure.Hubs;
using Planning.Application.Abstractions;

namespace Planning.Infrastructure.Services
{
    public class ReclamationNotificationService : IReclamationNotificationService
    {
        private readonly IHubContext<ReclamationHub> _hubContext;
        private readonly ILogger<ReclamationNotificationService> _logger;

        public ReclamationNotificationService(
            IHubContext<ReclamationHub> hubContext,
            ILogger<ReclamationNotificationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyManagersAsync(string titre, string message, string type)
        {
            Console.WriteLine($"?? NotifyManagersAsync ? group:managers | {titre}");
            await _hubContext.Clients.Group("managers").SendAsync(
                "ReclamationNotification",
                new { titre, message, type, createdAt = DateTime.UtcNow.ToString("o") }
            );
        }

        public async Task NotifyAuteurAsync(string auteurId, string titre, string message, string type)
        {
            Console.WriteLine($"?? NotifyAuteurAsync ? group:user_{auteurId} | {titre}");
            await _hubContext.Clients.Group($"user_{auteurId}").SendAsync(
                "ReclamationNotification",
                new { titre, message, type, createdAt = DateTime.UtcNow.ToString("o") }
            );
        }
    }
}