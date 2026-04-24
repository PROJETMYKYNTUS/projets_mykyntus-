using Microsoft.AspNetCore.SignalR;

namespace PlanningService.Hubs;

public class PlanningHub : Hub
{
    public async Task JoinUserGroup(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        Console.WriteLine($"✅ User {userId} rejoint le groupe");
    }
}