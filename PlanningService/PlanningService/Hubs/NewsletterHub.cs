using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PlanningService.Hubs
{
    [Authorize]
    public class NewsletterHub : Hub
    {
        /// <summary>
        /// Chaque employé rejoint son groupe personnel au chargement du dashboard.
        /// L'admin peut envoyer une newsletter ciblée à un groupe ou à tous.
        /// </summary>
        public async Task JoinGroup(string group)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
        }

        public async Task LeaveGroup(string group)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        }
    }
}