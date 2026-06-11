// src/Hubs/ReclamationHub.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PlanningService.Hubs
{
    [Authorize]
    public class ReclamationHub : Hub
    {
        private readonly ILogger<ReclamationHub> _logger;

        public ReclamationHub(ILogger<ReclamationHub> logger)
        {
            _logger = logger;
        }

        public async Task JoinManagerGroup()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "managers");
            Console.WriteLine($"✅ JoinManagerGroup — connId: {Context.ConnectionId}");
        }

        public async Task JoinUserGroup(string userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            Console.WriteLine($"✅ JoinUserGroup — userId: {userId}, connId: {Context.ConnectionId}");
        }

        public async Task LeaveGroup(string group)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        }
    }
}