using Microsoft.AspNetCore.SignalR;

namespace Planning.Infrastructure.Hubs;

public class PlanningHub : Hub
{
    public const string RhAdminsGroup = "rh_admins";

    public async Task JoinUserGroup(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        Console.WriteLine($"? User {userId} rejoint le groupe");
    }

    /// <summary>Groupe partagé RH / Admin — notifs demandes de changement (temps réel).</summary>
    public async Task JoinRhAdminGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, RhAdminsGroup);
        Console.WriteLine($"? Connexion {Context.ConnectionId} rejoint {RhAdminsGroup}");
    }
}